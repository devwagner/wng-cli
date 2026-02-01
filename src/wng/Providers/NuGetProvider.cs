using Spectre.Console;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using wng.Model;
using wng.Model.Dotnet;

namespace wng.Providers {
    public class NuGetProvider(bool debugMode = false) : IDisposable {

        private readonly bool _debugMode = debugMode;
        protected readonly NuGetProviderRepository _repository = new();

        public async Task<NuGetPackage> RefreshPackageAsync(NuGetPackage package, PackageVersion minimumFrameworkVersion, bool includePreRelease = false, CancellationToken ct = default) {
            NuGetRepositoryPackageResult packageResult = null;
            var allVersions = new ConcurrentBag<PackageVersion>();

            try {
                package.ClearFailure();
                packageResult = await _repository.GetNuGetPackageAsync(package.Name, includePreRelease, ct);
            }
            catch (Exception ex) { package.SetFailure(ex); return package; }

            if (!(packageResult.Versions?.Count > 0)) {
                package.SetFailure("Package could not be found in the official NuGet repository. (Custom sources are not yet supported!)");
                return package;
            }

            void addVersion(NuGetRepositoryVersionVersion versionResult) {
                var packageVersion = new PackageVersion(versionResult.Version.ToString(), versionResult.MetaData.Published.GetValueOrDefault().DateTime);
                if (!includePreRelease && packageVersion.IsPreRelease) return;

                if (versionResult.Vulnerabilities?.Count() > 0) {
                    packageVersion.Vulnerabilities = [.. versionResult.Vulnerabilities
                        .Select(v => new PackageVulnerability {
                            AdvisoryUrl = v.AdvisoryUrl.ToString(),
                            Severity = v.Severity.ToString(),
                        })];
                }

                if (versionResult.SupportedFrameworks?.Count > 0) {
                    List<PackageFramework> frameworks = [..versionResult.SupportedFrameworks];
                    if (versionResult.TargetFramework != null) {
                        var framework = versionResult.TargetFramework;
                        var shortName = framework.GetShortFolderName();
                        var nickName = shortName.Replace("core", "").Replace("standard", "");
                        frameworks.Add(new PackageFramework { Name = framework.DotNetFrameworkName, ShortName = shortName, NickName = nickName });
                    }
                    packageVersion.SetSupportedFrameworks(frameworks.OrderByDescending(f => f.Version));
                }

                allVersions.Add(packageVersion);
            }

            if (_debugMode) foreach (var version in packageResult.Versions) { addVersion(version); }
            else packageResult.Versions.AsParallel().ForAll(p => addVersion(p));

            var latestVersion = packageResult.Versions.OrderByDescending(p => p.Version).FirstOrDefault();
            package.ProjectUrl = latestVersion?.MetaData?.ProjectUrl?.ToString();
            package.PackageUrl = $"https://www.nuget.org/packages/{package.Name}";

            package.SetVersions([.. allVersions.OrderByDescending(v => v)]);
            return package;
        }

        public async Task<NuGetPackages> GetNuGetPackagesAsync(NuGetPackageRequest request, CancellationToken ct = default) {
            // Validate the request
            ArgumentNullException.ThrowIfNull(request, "GetNmpPackages=>(NuGetPackageRequest)request");
            ArgumentNullException.ThrowIfNull(request.ProjectList, "GetNmpPackages=>(NuGetPackageRequest)request.(NpmPackages)ProjectList");

            // Get the project file(s) and associated packages
            var result = request.ProjectList;
            if (result.AllPackages.Count == 0) return result;
            result.Filter(request.InclusivePackages, request.IgnorePackages);

            // Execute the prepare action on each package if provided
            if (request.PrepareAction != null) {
                foreach (var pkg in result.AllPackages) {
                    request.PrepareAction(pkg);
                }
            }

            // Refresh each package info from NuGet registry
            if (!_debugMode) {
                // Without debug mode, we execute in parallel
                var tasks = result.AllPackages.Select(pkg => { return RefreshPackageAsync(pkg, request.MinimumFrameworkVersion, request.IncludePreRelease, ct); });
                await Task.WhenAll(tasks);
                tasks.AsParallel().Select(t => t.Result).ToList();
            }
            else {
                // With debug mode, we execute sequentially 
                foreach (var pkg in result.AllPackages) {
                    await RefreshPackageAsync(pkg, request.MinimumFrameworkVersion, request.IncludePreRelease, ct);
                }
            }

            // Return the list of processed packages
            result.UpdatePackageProjectsNames();
            return result;
        }

        public static NuGetPackages GetNuGetPackageProjects(string projectFilePath) {
            var result = new NuGetPackages();
            var projectFilePathInfo = new FileInfo(projectFilePath);

            // If the path provided is for an individual project file
            if (projectFilePathInfo.Exists) {
                var projectFile = new NuGetProjectFile(projectFilePathInfo.Directory.Name, projectFilePathInfo.FullName);
                var project = projectFile.GetProject();
                result.AddProject(project);
            }

            // Otherwise, this is a solution folder and we must locate the projects
            else {

                // Check if the directory exists
                var solutionDirectory = new DirectoryInfo(projectFilePath);
                if (!solutionDirectory.Exists) return result;

                // First check if we have a Packages.props file in the folder
                var packagesPropsFile = FindPackagesPropFile(solutionDirectory);
                if (packagesPropsFile != null) {
                    var packagesPropsProjectFile = new NuGetProjectFile(packagesPropsFile.Directory.Name, packagesPropsFile.FullName);
                    var packagesPropsProject = packagesPropsProjectFile.GetProject();
                    result.AddProject(packagesPropsProject);
                }

                // Then locate all .csproj files in the directory and subdirectories
                else {
                    var projectFiles = solutionDirectory.GetFiles("*.csproj", SearchOption.AllDirectories);
                    foreach (var projFile in projectFiles) {
                        var projectFile = new NuGetProjectFile(projFile.Directory.Name, projFile.FullName);
                        var project = projectFile.GetProject();
                        result.AddProject(project);
                    }
                }
            }
            return result;
        }

        public static FileInfo FindPackagesPropFile(DirectoryInfo solutionDirectory, bool forwardSearch = true) {
            var searchOption = forwardSearch ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var packagesPropsFile = solutionDirectory.GetFiles("*.Packages.props", searchOption).FirstOrDefault();
            if (packagesPropsFile != null) {
                return packagesPropsFile;
            }
            if (solutionDirectory.Parent != null) return FindPackagesPropFile(solutionDirectory.Parent, false);
            return null;
        }

        public static async Task<int> RunDotNetCommandAsync([Required] DotNetCommandRequest request, CancellationToken ct = default) {
            if (string.IsNullOrEmpty(request.Command)) throw new NoNullAllowedException("Invalid dotnet command. (<DotNetCommandRequest>request.Command)");

            var projectFilePath = new FileInfo(request.ProjectFilePath);
            if (!projectFilePath.Exists) throw new FileNotFoundException("The specified project file was not found.", request.ProjectFilePath);

            var processStartInfo = new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = request.Command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = projectFilePath.Directory.FullName,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            // Force command to behave as if in an interactive terminal
            processStartInfo.Environment["FORCE_COLOR"] = "1";
            processStartInfo.Environment["CI"] = "false";

            using var process = new Process { StartInfo = processStartInfo };
            if (!process.Start()) throw new Exception($"Failed to start dotnet {request.Command} process.");

            var outputTask = Task.Run(async () => {
                string line;
                while ((line = await process.StandardOutput.ReadLineAsync(ct)) != null) {
                    request.OnDataOutput?.Invoke(line);
                }
            }, ct);

            var errorTask = Task.Run(async () => {
                string line;
                while ((line = await process.StandardError.ReadLineAsync(ct)) != null) {
                    request.OnErrorOutput?.Invoke(line);
                }
            }, ct);

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync(ct);
            return process.ExitCode;
        }

        public static async Task<NuGetPackageUpdateResult> UpdatePackageVersionsAsync(NuGetPackageUpdateRequest request, CancellationToken ct = default) {
            // Validate input
            ArgumentNullException.ThrowIfNull(request?.ProjectList, "UpdatePackageVersions=>(List<Package>)request.ProjectList");
            if (request?.ProjectList?.Projects?.Any(p => p.Packages?.Any() != true) ?? true) throw new ArgumentException("No projects found to update.");

            // Initialize the result object
            var updateResult = new NuGetPackageUpdateResult();

            // Iterate through each project to update package versions
            foreach (var project in request.ProjectList.Projects) {

                // Read the project file lines
                var fileInfo = new FileInfo(project.FilePath);
                if (!fileInfo.Exists) throw new FileNotFoundException("Could not find file.", fileInfo.FullName);
                var lines = File.ReadAllLines(fileInfo.FullName).ToList();

                // Filter package list to remove failed and invalid packages
                var packageList = project.Packages.Where(p =>
                    p.CurrentVersion is not null &&
                    !p.HasFailed &&
                    !string.IsNullOrWhiteSpace(p.CurrentVersion.Version))
                .OrderBy(p => p.Order)
                .ToList();
                if (!(packageList.Count > 0)) continue;

                // Update each package version in the file lines
                foreach (var package in packageList) {
                    try {
                        // Check if the version must be updated
                        if (!package.HasVersionMismatch) { updateResult.Add(package, false, false); continue; }

                        // Get the current and new version
                        var newVersion = package.DesiredVersion.ParsedVersion;
                        var currentVersion = package.CurrentVersion.Version;

                        // Find the line index of the package in the file
                        var lineIndex = lines.FindFirstIndex((line) => line.Contains($"\"{package.Name}\"") && line.Contains($"{currentVersion}"));
                        if (lineIndex == -1) { updateResult.Add(package, false, true, "Could not find package in file."); continue; }

                        // Update the version in the line
                        lines[lineIndex] = lines[lineIndex].ReplaceNuGetPackageVersion(package.Name, newVersion);
                    }
                    catch (Exception ex) { updateResult.Add(package, false, true, $"{ex.Message}"); }
                }

                // Write the updated lines back to the package.json file
                await File.WriteAllLinesAsync(fileInfo.FullName, lines, ct);
            }

            return updateResult;
        }

        public void Dispose() {
            _repository.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
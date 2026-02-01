using Spectre.Console;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using wng.Model;
using wng.Model.Npm;

namespace wng.Providers {
    public class NpmProvider : IDisposable {

        private readonly bool _debugMode = false;
        protected readonly HttpClient _httpClient;
        protected readonly HttpClientHandler _httpClientHandler = new() { AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate };

        public NpmProvider(bool debugMode = false) {
            _debugMode = debugMode;
            _httpClient = new HttpClient(_httpClientHandler) {
                Timeout = TimeSpan.FromSeconds(30),
                BaseAddress = new Uri("https://registry.npmjs.org/"),
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "wng-cli/1.0");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Keep-Alive", "timeout=60, max=1000");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        }

        public async Task<NpmPackages> GetNmpPackagesAsync(NpmPackageRequest request, CancellationToken ct = default) {
            // Validate the request
            ArgumentNullException.ThrowIfNull(request, "GetNmpPackages=>(NpmPackageRequest)request");
            ArgumentNullException.ThrowIfNull(request.PackageJsonFileOrFolderPath, "GetNmpPackages=>(NpmPackageRequest)request.(string)PackageJsonFilePath");

            // Get all package.json projects from the provided path
            var result = GetNpmPackageProjects(request.PackageJsonFileOrFolderPath);
            if (result.AllPackages.Count == 0) return result;
            result.Filter(request.InclusivePackages, request.IgnorePackages);

            // Execute the prepare action if provided
            if (request.PrepareAction != null)
                foreach (var pkg in result.AllPackages)
                    request.PrepareAction(pkg);

            // Refresh each package info from NPM registry
            if (!_debugMode) {
                // Without debug mode, we execute in parallel
                var tasks = result.AllPackages.Select(pkg => { return RefreshPackageAsync(pkg, request.IncludePreRelease, ct); });
                await Task.WhenAll(tasks);
                tasks.AsParallel().Select(t => t.Result).ToList();
            }
            else {
                // With debug mode, we execute sequentially 
                foreach (var pkg in result.AllPackages) {
                    await RefreshPackageAsync(pkg, request.IncludePreRelease, ct);
                }
            }

            // Return the list of processed packages
            result.UpdatePackageProjectsNames();
            return result;
        }

        private async Task<NpmPackage> RefreshPackageAsync(NpmPackage package, bool includePreRelease = false, CancellationToken ct = default) {
            package.ClearFailure();

            // Fetch package info from NPM registry
            var response = await _httpClient.GetAsync(package.Name, ct);
            if (!response.IsSuccessStatusCode) {
                package.SetFailure($"Failed to fetch package info from NPM registry. Status code: {response.StatusCode}\n\r{response.ReasonPhrase}");
                return package;
            }

            // Read and parse the response content
            var content = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(content)) {
                package.SetFailure("NPM registry returned empty content.");
                return package;
            }

            try {
                // Deserialize the JSON content to get package info
                var npmPackageInfo = System.Text.Json.JsonSerializer.Deserialize<NpmPackageInfo>(content);
                if (npmPackageInfo == null) {
                    package.SetFailure("Failed to deserialize NPM package info.");
                    return package;
                }

                // Process versions and set package properties
                var allVersions = npmPackageInfo.Time.Keys
                    .Where(v => v != "created" && v != "modified")
                    .Select(v => new PackageVersion(v, npmPackageInfo.Time[v]))
                    .Where(v => (package.CurrentVersion?.Version?.Contains('-') ?? false) || includePreRelease == true || !v.IsPreRelease)
                    .OrderByDescending(v => v)
                    .ToList();

                // Set package versions and URLs
                package.SetVersions(allVersions);
                package.ProjectUrl = npmPackageInfo.HomePage;
                package.PackageUrl = $"https://www.npmjs.com/package/{package.Name}";
            }
            catch (Exception ex) { package.SetFailure($"Exception occurred while processing NPM package info: {ex.Message}"); }

            // Return the refreshed package
            return package;
        }

        public static NpmPackages GetNpmPackageProjects(string projectFilePath) {
            var result = new NpmPackages();
            var standaloneFile = new FileInfo(projectFilePath);

            // If the path provided is for an individual package.json file
            if (standaloneFile.Exists) {
                var projectFile = new NpmProjectFile(standaloneFile.Directory.Name, standaloneFile.FullName);
                var project = projectFile.GetProject();
                result.AddProject(project);
            }

            // Otherwise, this is a solution folder and we must locate the projects
            else {
                // Check if the directory exists
                var solutionDirectory = new DirectoryInfo(projectFilePath);
                if (!solutionDirectory.Exists) return result;

                // Find all package.json files in the directory and subdirectories
                var packageJsonFiles = solutionDirectory.GetFiles("package.json", SearchOption.AllDirectories);
                foreach (var packageJsonFile in packageJsonFiles) {
                    if (packageJsonFile.Directory.Name.StartsWith('.') ||
                        packageJsonFile.Directory.FullName.Contains("node_modules", StringComparison.InvariantCultureIgnoreCase)) continue;

                    var projectFile = new NpmProjectFile(packageJsonFile.Directory.Name, packageJsonFile.FullName);
                    var project = projectFile.GetProject();
                    result.AddProject(project);
                }
            }

            return result;
        }


        public static async Task<NpmPackageUpdateResult> UpdatePackageVersionsAsync(NpmPackageUpdateRequest request, CancellationToken ct = default) {
            // Validate input
            ArgumentNullException.ThrowIfNull(request?.ProjectList, "UpdatePackageVersions=>(List<Package>)request.ProjectList");
            if (request?.ProjectList?.Projects?.Any(p => p.AllDependencies?.Any() != true) ?? true) throw new ArgumentException("No projects found to update.");

            // Initialize the result object
            var updateResult = new NpmPackageUpdateResult();

            // Iterate through each project to update package versions
            foreach (var project in request.ProjectList.Projects) {

                // Read the package.json file lines
                var fileInfo = new FileInfo(project.FilePath);
                if (!fileInfo.Exists) throw new FileNotFoundException("Could not find file.", fileInfo.FullName);
                var lines = File.ReadAllLines(fileInfo.FullName).ToList();

                // Filter package list to remove failed and invalid packages
                var packageList = project.AllDependencies.Where(p =>
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
                        var newVersion = package.DesiredVersion;
                        var currentVersion = package.CurrentVersion.Version;

                        // Find the line index of the package in the file
                        var lineIndex = lines.FindFirstIndex((line) => line.Contains($"\"{package.Name}\"") && line.Contains($"{currentVersion}"));
                        if (lineIndex == -1) { updateResult.Add(package, false, true, "Could not find package in file."); continue; }

                        // Update the package version in the line
                        lines[lineIndex] = lines[lineIndex].ReplaceVersionInPackageText(newVersion.Version);
                        updateResult.Add(package, true, false);
                    }
                    catch (Exception ex) { updateResult.Add(package, false, true, $"{ex.Message}"); }
                }

                // Write the updated lines back to the package.json file
                await File.WriteAllLinesAsync(fileInfo.FullName, lines, ct);
            }
            return updateResult;
        }

        public static async Task<int> RunNpmCommandAsync([Required] NpmCommandRequest request, CancellationToken ct = default) {

            if (string.IsNullOrEmpty(request.Command)) throw new NoNullAllowedException("Invalid npm command. (<NpmCommandRequest>request.Command)");

            var packageJsonFile = new FileInfo(request.PackageJsonFilePath);
            if (!packageJsonFile.Exists) throw new FileNotFoundException("Could not find package.json file.", packageJsonFile.FullName);

            var isWindows = OperatingSystem.IsWindows();
            var npmInstance = await FindNpmPathAsync(packageJsonFile, isWindows, ct);
            request.NpmLocationAction?.Invoke(npmInstance?.Length > 0, npmInstance ?? "We could not find npm installed in your system. Please refer to: https://docs.npmjs.com/downloading-and-installing-node-js-and-npm");
            if (!(npmInstance?.Length > 0)) return -1;

            var processStartInfo = new ProcessStartInfo {
                FileName = npmInstance,
                Arguments = request.Command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = packageJsonFile.Directory.FullName,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            // Force command to behave as if in an interactive terminal
            processStartInfo.Environment["FORCE_COLOR"] = "1";
            processStartInfo.Environment["CI"] = "false";

            using var process = new Process { StartInfo = processStartInfo };
            if (!process.Start()) throw new Exception($"Failed to start npm {request.Command} process.");

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

        public static async Task<NpmAuditResult> GetNpmAuditReportAsync(string packageJsonFilePath, CancellationToken ct = default) {
            var npmCommandJsonLines = new List<string>();

            var npmCommandRequest = new NpmCommandRequest(){
                PackageJsonFilePath = packageJsonFilePath,
                Command = "audit --json",
                OnDataOutput = output => npmCommandJsonLines.Add(output),
            };

            var npmCommandResult = await RunNpmCommandAsync(npmCommandRequest, ct);
            var npmAuditJson = string.Join(Environment.NewLine, npmCommandJsonLines);
            return System.Text.Json.JsonSerializer.Deserialize<NpmAuditResult>(npmAuditJson);
        }

        protected static async Task<string> FindNpmPathAsync(FileInfo packageJsonFile, bool isWindows, CancellationToken ct) {
            //First, check if the npm exists in the local node modules
            var npmFilePath = isWindows ? "npm.cmd" : "npm"; ;
            var npmFile = new FileInfo(Path.Combine(packageJsonFile.Directory.FullName, "node_modules", npmFilePath));
            if (npmFile.Exists) return npmFile.FullName;

            //Otherwise list the computer installed npm instances
            var npmPaths = await FindGlobalNpmPathAsync(isWindows, ct);

            //If we are on windows, prefer the user specific global npm version
            var windowsUserNpm = npmPaths.FirstOrDefault(p => p.Contains("users", StringComparison.InvariantCultureIgnoreCase) && p.EndsWith(".cmd", StringComparison.InvariantCultureIgnoreCase));
            var windowsNodeNpm = npmPaths.FirstOrDefault(p => p.Contains("nodejs", StringComparison.InvariantCultureIgnoreCase) && p.EndsWith(".cmd", StringComparison.InvariantCultureIgnoreCase));
            if (isWindows) return windowsUserNpm ?? windowsNodeNpm;

            //If we are not on windows, prefer the user specific global npm version
            var userNpm = npmPaths.FirstOrDefault(p => p.Contains("users", StringComparison.InvariantCultureIgnoreCase) && p.EndsWith("npm", StringComparison.InvariantCultureIgnoreCase));
            var nodeNpm = npmPaths.FirstOrDefault(p => p.Contains("nodejs", StringComparison.InvariantCultureIgnoreCase) && p.EndsWith("npm", StringComparison.InvariantCultureIgnoreCase));
            return userNpm ?? nodeNpm;
        }

        protected static async Task<IEnumerable<string>> FindGlobalNpmPathAsync(bool isWindows, CancellationToken ct) {
            var processStartInfo = new ProcessStartInfo {
                FileName = isWindows ? "cmd.exe" : "/bin/bash",
                Arguments = isWindows ? "/c where npm" : "-c \"which npm\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        }

        public void Dispose() {
            _httpClient.Dispose();
            _httpClientHandler.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

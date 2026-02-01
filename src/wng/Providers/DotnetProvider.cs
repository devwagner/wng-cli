using Spectre.Console;
using System.Data;
using System.Diagnostics;
using System.Net.Http.Json;
using wng.Model.Dotnet;

namespace wng.Providers {
    public class DotnetProvider : IDisposable {

        private readonly bool _debugMode = false;
        protected readonly HttpClient _httpClient;
        protected readonly HttpClientHandler _httpClientHandler = new() { AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate };

        public DotnetProvider(bool debugMode = false) {
            _debugMode = debugMode;
            _httpClient = new HttpClient(_httpClientHandler) { Timeout = TimeSpan.FromSeconds(30) };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "wng-cli/1.0");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Keep-Alive", "timeout=60, max=1000");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        }

        public static async Task<DotNetProject> GetDotNetProjectAsync(string projectFile, CancellationToken ct = default) {
            var frameworkVersion = await GetFrameworkVersionAsync(projectFile);
            var nugetProjectFile = new DotNetProject(new FileInfo(projectFile), frameworkVersion);
            return nugetProjectFile;
        }

        public async Task<DotNetProjects> GetDotNetProjects(string solutionPath, CancellationToken ct = default) {

            var projectFile = new FileInfo(solutionPath);
            if (projectFile.Exists && projectFile.Extension.EndsWith("proj", StringComparison.OrdinalIgnoreCase)) {
                var project = await GetDotNetProjectAsync(solutionPath, ct);
                return new DotNetProjects(projectFile.Name, [project]);
            }

            DirectoryInfo solutionFolder;
            if (projectFile.Exists && !projectFile.Extension.EndsWith("proj", StringComparison.OrdinalIgnoreCase)) solutionFolder = projectFile.Directory;
            else solutionFolder = new DirectoryInfo(solutionPath);

            if (!solutionFolder.Exists) return new DotNetProjects("Unknown");

            var projectFiles = solutionFolder.GetFiles("*.*proj", SearchOption.AllDirectories);

            if (_debugMode) {
                var projectList = new List<DotNetProject>();
                foreach (var file in projectFiles) {
                    var project = await GetDotNetProjectAsync(file.FullName, ct);
                    projectList.Add(project);
                }
                return new DotNetProjects(solutionFolder.Name, projectList);
            }

            var projects = projectFiles.Select(async pf => await GetDotNetProjectAsync(pf.FullName, ct));
            return new DotNetProjects(solutionFolder.Name, await Task.WhenAll(projects));
        }

        public static async Task<string> GetFrameworkVersionAsync(string filePath) {
            if (filePath.EndsWith("proj", StringComparison.InvariantCultureIgnoreCase)) {
                var projFile = new FileInfo(filePath);
                if (projFile.Exists) {
                    var projFileContents = await File.ReadAllTextAsync(projFile.FullName);
                    var targetFramework = projFileContents.GetTargetDotNetFramework();
                    if (!string.IsNullOrEmpty(targetFramework))
                        return targetFramework;
                }
            }
            return null;
        }

        public async Task<DotnetFrameworkVersions> GetDotNetVersionsAsync(CancellationToken ct = default) {
            var result = new DotnetFrameworkVersions();
            try {
                var installedVersions = await GetInstalledDotnetVersionsAsync(ct);

                var indexJsonEndpoint = new Uri("https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json");
                var indexJson = await _httpClient.GetFromJsonAsync<DotnetReleasesIndex>(indexJsonEndpoint, ct);
                if (indexJson == null || !(indexJson?.ReleasesIndex?.Count > 0)) {
                    result.SetFailure("Failed to retrieve .NET releases index.");
                    return result;
                }

                var versions = new List<DotnetFrameworkVersion>();
                async Task<DotnetFrameworkVersion> buildVersion(DotnetReleaseInfo releaseInfo, CancellationToken ct = default) {
                    if (releaseInfo.ReleasesJsonUrl?.Length > 0) {
                        var releasesJsonEndpoint = new Uri(releaseInfo.ReleasesJsonUrl);
                        releaseInfo.Metadata = await _httpClient.GetFromJsonAsync<DotnetReleaseMetadata>(releasesJsonEndpoint, ct);
                    }
                    return new DotnetFrameworkVersion(releaseInfo, installedVersions);
                }

                if (_debugMode) {
                    foreach (var releaseInfo in indexJson.ReleasesIndex) {
                        var version = await buildVersion(releaseInfo, ct);
                        versions.Add(version);
                    }
                }
                else {
                    var versionsRaskResult = indexJson.ReleasesIndex.AsParallel().Select(async releaseInfo => await buildVersion(releaseInfo, ct));
                    var versionsResult = await Task.WhenAll(versionsRaskResult);
                    versions.AddRange(versionsResult);
                }

                result.InstalledVersions = installedVersions;
                result.NetCore.Versions.AddRange(versions.OrderByDescending(v => v.ChannelVersion));
            }
            catch (Exception ex) { result.SetFailure($"Exception occurred while processing Dotnet versions: {ex.Message}"); }
            return result;
        }

        public static async Task<DotnetInstalledVersions> GetInstalledDotnetVersionsAsync(CancellationToken ct = default) {
            var result = new DotnetInstalledVersions();

            try {
                // Get installed SDKs
                var sdksOutput = await RunDotnetInfoCommandAsync("--list-sdks", ct);
                result.Sdks = sdksOutput.ParseDotnetSdkOutput();

                // Get installed runtimes
                var runtimesOutput = await RunDotnetInfoCommandAsync("--list-runtimes", ct);
                result.Runtimes = runtimesOutput.ParseDotnetRuntimeOutput();

                result.IsInstalled = result.Sdks.Count > 0 || result.Runtimes.Count > 0;
            }
            catch (Exception ex) {
                result.SetFailure($"Failed to get installed .NET versions: {ex.Message}");
            }

            return result;
        }

        public static async Task<List<string>> InstallDotnetVersion(DotnetFrameworkVersionRelease version, bool runtime, CancellationToken ct) {

            var files = runtime ? version.Release.Runtime.Files : version.Release.Sdk.Files;
            var isWindows = OperatingSystem.IsWindows();
            var isMacOs = OperatingSystem.IsMacOS();
            var isLinux = OperatingSystem.IsLinux();

            var fileUrls = new Dictionary<string, string>();

            foreach (var file in files) {
                if (isWindows && !file.Rid.StartsWith("win")) { continue; }
                else if (isMacOs && !file.Rid.StartsWith("osx")) { continue; }
                else if (isLinux && !file.Rid.StartsWith("linux")) { continue; }
                //AnsiConsole.MarkupLine($"    [Gray]- {file.Rid} : {file.Url.Split('/').Last()}[/]");
                fileUrls.Add(file.Url, file.Url.Split('/').Last());
            }

            var cancelOptionKey = "- Cancel Install Operation";
            var selectedFile = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[blue]? Confirm:[/] [yellow]Multiple installers[/] have been found. Please, select which installer would you like to open:")
                    .AddChoices(fileUrls.Keys.Concat([cancelOptionKey]))
                    .UseConverter(f => fileUrls.TryGetValue(f, out string value) ? "- " + value : f)
            );

            if (string.IsNullOrEmpty(selectedFile) || selectedFile == cancelOptionKey) {
                return ["Operation Cancelled!"];
            }

            FileInfo selectedFileLocal = null;
            await AnsiConsole.Status().Spinner(Spinner.Known.Moon).SpinnerStyle(Style.Parse("Gold1")).StartAsync("Downloading [blue]dotnet[/] installer file...", async ctx => {
                selectedFileLocal = await DownloadDotnetInstallerAsync(selectedFile, ct);
            });
            if (selectedFileLocal?.Exists != true) return [$"Was not possible to download the dotnet framework installer file: {selectedFile}"];

            if (isWindows) {
                var processName = selectedFileLocal.FullName;
                var processArgs = string.Empty;
                if (!selectedFileLocal.Extension.EndsWith("exe", StringComparison.InvariantCultureIgnoreCase) &&
                   !selectedFileLocal.Extension.EndsWith("msi", StringComparison.InvariantCultureIgnoreCase)) {
                    processName = "explorer.exe";
                    processArgs = $"{selectedFileLocal.FullName}";
                }

                var processStartInfo = new ProcessStartInfo {
                    FileName = processName,
                    Arguments = processArgs,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                using var process = new Process { StartInfo = processStartInfo };
                if (!process.Start()) { throw new Exception($"Failed to start: {selectedFileLocal.FullName}"); }
            }
            else {
                //TODO: Add follow up actions for linux and macos
            }

            return [$"Successfully downloaded dotnet installer file", selectedFileLocal.FullName];
        }

        private static async Task<FileInfo> DownloadDotnetInstallerAsync(string downloadUrl, CancellationToken ct) {

            var uri = new Uri(downloadUrl);
            var fileName = Path.GetFileName(uri.LocalPath);

            var destinationFilePath = Path.Combine(Path.GetTempPath(), fileName);
            if (File.Exists(destinationFilePath)) File.Delete(destinationFilePath);

            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            await contentStream.CopyToAsync(fileStream, ct);

            return new FileInfo(destinationFilePath);
        }



        private static async Task<string> RunDotnetInfoCommandAsync(string arguments, CancellationToken ct) {
            var outputLines = new List<string>();

            var processStartInfo = new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };

            if (!process.Start()) {
                throw new Exception("Failed to start dotnet process.");
            }

            var outputTask = Task.Run(async () => {
                string line;
                while ((line = await process.StandardOutput.ReadLineAsync(ct)) != null) {
                    outputLines.Add(line);
                }
            }, ct);

            var errorTask = Task.Run(async () => {
                string line;
                while ((line = await process.StandardError.ReadLineAsync(ct)) != null) {
                    // Ignore errors for now
                }
            }, ct);

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync(ct);

            return string.Join(Environment.NewLine, outputLines);
        }

        public void Dispose() {
            _httpClient.Dispose();
            _httpClientHandler.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

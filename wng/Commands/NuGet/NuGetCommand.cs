using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using wng.Model;
using wng.Model.Dotnet;
using wng.Model.Shared;
using wng.Providers;

namespace wng.Commands.NuGet {
    internal class NuGetCommand : AsyncCommand<NuGetCommand.Settings> {

        public class Settings : CommandSettings, IPackageCommandSettings {

            public static readonly string CommandKey = "nuget";
            public static readonly string CommandDescription = "Manage and evaluate NuGet package versions";

            [CommandArgument(0, "[path]")]
            [Description("(Optional) [Gray]The path to the csproj or packages.props file or the solution folder it resides. If not specified, the current working directory will be used.[/]")]
            public string Path { get; init; } = Directory.GetCurrentDirectory();

            [CommandOption("-p|--packages <packages>")]
            [Description("""[Gray]A comma-separated list of names or partial names to filter the packages during analysis. Ex. "package1,package2,package3".[/]""")]
            [DefaultValue(null)]
            public string PackageNames { get; init; } = string.Empty;

            [CommandOption("-g|--ignore <packages>")]
            [Description("""[Gray]A comma-separated list of names or partial names to filter the packages during analysis. Ex. "package1,package2,package3".[/]""")]
            public string IgnorePackageNames { get; init; } = string.Empty;

            [CommandOption("-m|--major <number>")]
            [Description("[Gray]Find the latest version of the specified major number.[/]")]
            [DefaultValue(null)]
            public int? Major { get; init; }

            [CommandOption("-n|--minor")]
            [Description("[Gray]Only evaluate the lastest version of the current major.[/]")]
            [DefaultValue(false)]
            public bool Minor { get; init; }

            [CommandOption("-i|--install")]
            [Description("[Gray]Run the [DarkOrange]restore[/] command even if you don't provide the [Blue]--update[/] argument. This is useful when checking for vulnerabilities when the packages haven't being installed yet.[/]")]
            public bool Install { get; set; }

            [CommandOption("-b|--build")]
            [Description("[Gray]Run the [DarkOrange]build[/] command even if you don't provide the [Blue]--update[/] argument. This is useful to check if your code is compatible with the installed packages.[/]")]
            public bool Build { get; set; }

            [CommandOption("-r|--restore")]
            [Description("[Gray]Run the [DarkOrange]restore[/] command before evaluating packages. This is useful to install the updated packages right away.[/]")]
            public bool Restore { get; set; }

            [CommandOption("-u|--update")]
            [Description("[Gray]Update the packages to desired version. Latest version by default, lastest of the current Major when the [blue]--minor[/] option is specified or latest requested version when the [blue]--major <n>[/] option is specified.[/]")]
            [DefaultValue(false)]
            public bool Update { get; init; } = false;

            [CommandOption("--pre")]
            [Description("[Gray]Include prerelease versions when evaluating the latest version. (Alpha, Beta, Release Candidate, Nightly Builds, etc).[/]")]
            [DefaultValue(false)]
            public bool IncludePreRelease { get; init; }

            [CommandOption("--url")]
            [Description("[Gray]Include package URL in the list.[/]")]
            public bool ShowUrl { get; init; }

            [CommandOption("--projectUrl")]
            [Description("[Gray]Include package project url in the list.[/]")]
            public bool ShowProjectUrl { get; set; }

            [CommandOption("--versionUrl")]
            [Description("[Gray]Include package version url in the list.[/]")]
            public bool ShowVersionUrl { get; set; }

            [CommandOption("--silent")]
            [Description("[Gray]When using the [Blue]--update[/] argument, we won't ask for confirmation to continue or to install the packages. Both operations will be executed.[/]")]
            [DefaultValue(false)]
            public bool Silent { get; set; }

            [CommandOption("--debug")]
            [Description("[Gray]Only to be used during [Blue]development[/] to include additional columns in the list for debugging purposes.[/]")]
            [DefaultValue(false)]
            public bool Debug { get; set; }

            public string ProjectFolderPath { get; set; }
            public List<string> InclusivePackageList => [.. PackageNames?.Split(',', StringSplitOptions.RemoveEmptyEntries)];
            public List<string> IgnorePackageList => [.. IgnorePackageNames?.Split(',', StringSplitOptions.RemoveEmptyEntries)];
        }

        public static void Register(IConfigurator config) {
            config.AddCommand<NuGetCommand>(Settings.CommandKey)
            .WithDescription(Settings.CommandDescription)
            .WithExample("nuget")
            .WithExample("nuget", "--update")
            .WithExample("nuget", "--pre")
            .WithExample("nuget", "--minor")
            .WithExample("nuget", "--minor", "--update")
            .WithExample("nuget", "--packages", """System.Data,Microsoft""")
            .WithExample("nuget", "--packages", """Azure""", "--major", "4")
            .WithExample("nuget", "\"C:\\MyProject\"", "--packages", "\"Azure.Core,Microsoft.Graph\"\"", "--update");
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct) {
            App.PrintAppWelcome();

            if (settings.Update) AnsiConsole.MarkupLine("[blue]֍ Command: Upgrade project NuGet package versions[/]");
            else AnsiConsole.MarkupLine("[blue]֍ Command: Evaluate project referenced NuGet package versions[/]");

            NuGetPackages projectList = null;
            AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("yellow")).Start("Locating packages...", ctx => {
                projectList = NuGetProvider.GetNuGetPackageProjects(settings.Path);
            });

            if (projectList == null || projectList.Projects.Count == 0) {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] project file not found. Please make sure you are in the right directory or specify a valid path using the [blue]--path[/] option");
                return await Task.FromResult(-1);
            }
            AnsiConsole.MarkupLine($"[green]✓ Success:[/] File: [yellow]{projectList.Projects.Count} package file(s)[/] found!");

            var firstProject = projectList.Projects.FirstOrDefault();
            var firstProjectDirectory = new DirectoryInfo(Path.GetDirectoryName(firstProject.FilePath));
            var minimumFrameworkVersion = await NuGetCommandHandler.GetLeastProjectVersionInSolutionAsync(firstProjectDirectory.FullName, ct);
            if (minimumFrameworkVersion?.ToString()?.Length > 0) {
                AnsiConsole.MarkupLine($"[green]✓ Success:[/] Found the least framework version in your solution: [blue]net{minimumFrameworkVersion}[/]");
                AnsiConsole.MarkupLine($"[DarkOrange]? Warning:[/] The package version result list will be filter to support at least this version");
            }            

            using var nuGetProvider = new NuGetProvider(settings.Debug);
            await NuGetCommandHandler.GetPackagesAsync(settings, nuGetProvider, projectList, minimumFrameworkVersion, ct);

            if (projectList == null || projectList.AllPackages.Count == 0) {
                AnsiConsole.MarkupLine("[DarkOrange]? Warning:[/] No packages found in the project file(s)");
                return await Task.FromResult(-1);
            }
            AnsiConsole.MarkupLine($"[green]✓ Success:[/] A total of [CadetBlue]{projectList.AllPackages.Count}[/] packages where found!");

            if (settings.Restore) {
                var restoreResult = await ExecuteNuGetRestoreCommandAsync(settings, ct);
                if (restoreResult < 0) return restoreResult;
            }

            if (settings.Update) return await ExecutePackageUpdateCommandAsync(settings, projectList, minimumFrameworkVersion, nuGetProvider, ct);
            else if (settings.Build && !settings.Install) {
                await ExecutePackageListCommand(settings, projectList, ct);
                return await ExecuteDotNetBuildCommand(settings, null, ct);
            }
            return await ExecutePackageListCommand(settings, projectList, ct);
        }

        public static async Task<int> ExecutePackageListCommand(Settings settings, NuGetPackages projectList, CancellationToken ct = default) {
            var commandResult = 0;

            await AnsiConsole.Status().Spinner(Spinner.Known.Moon).SpinnerStyle(Style.Parse("yellow")).StartAsync("Analysing results...", async ctx => {
                Extensions.ReasonableWait();
                AnsiConsole.MarkupLine($"[green]✓ Success:[/] Analysis complete for [CadetBlue]{projectList.AllPackages.Count}[/] packages");

                if (projectList.AllPackages.Any(p => p.HasVersionMismatch)) {
                    AnsiConsole.MarkupLine("[DarkOrange]? Warning:[/] Some of your packages have a new version avaliable!");
                    AnsiConsole.MarkupLine("          [gray] You can update those packages running the same command with [blue]-u[/] or [blue]--update[/][/]");
                    AnsiConsole.MarkupLine("          [gray] Just be aware, that UI frameworks sometimes requires you to use their own CLI for upgrades[/]");
                }

                if (projectList.AllPackages.Any(p => p.IsCurrentVersionInvalid)) {
                    AnsiConsole.MarkupLine("[red]✗ Problem:[/] You have one or more packages with an invalid version!");
                }

                if (projectList.AllPackages.Any(p => p.HasFailed)) {
                    AnsiConsole.MarkupLine("[red]✗ Problem:[/] We encountered issues when trying to load one of more packages!");
                }
            });

            NuGetCommandHandler.PrintPackagesTable(settings, projectList);
            if (settings.Install) commandResult = await ExecuteNuGetRestoreCommandAsync(settings, ct);
            await CheckForVulnerabilities(projectList, ct);

            return await Task.FromResult(commandResult);
        }

        public static async Task<int> ExecutePackageUpdateCommandAsync(Settings settings, NuGetPackages projectList, PackageVersion minimumFrameworkVersion, NuGetProvider nugetProvider, CancellationToken ct = default) {
            var packagesToUpdateCount = projectList.AllPackages.Count(p => p.CurrentVersion is not null && p.HasVersionMismatch && !p.HasFailed);
            AnsiConsole.MarkupLine($"[green]✓ Success:[/] Analysis complete for [CadetBlue]{projectList.AllPackages.Count}[/] packages");

            if (packagesToUpdateCount == 0) {
                AnsiConsole.MarkupLine($"[DarkOrange]? Warning:[/] No changes are needed for any of your packages at this time");
                return await Task.FromResult(0);
            }

            if (!settings.Silent && !AnsiConsole.Confirm($"[blue]? Confirm:[/] We have found [darkorange]{packagesToUpdateCount}[/] packages to update. Would you like to continue?")) {
                AnsiConsole.MarkupLine("[purple]! Warning:[/] Operation cancelled!");
                return await Task.FromResult(0);
            }

            NuGetPackageUpdateResult updateResult = null;
            await AnsiConsole.Status().Spinner(Spinner.Known.Moon).SpinnerStyle(Style.Parse("yellow")).StartAsync("Updating your packages...", async ctx => {
                Extensions.ReasonableWait(1000);
                var updateRequest = new NuGetPackageUpdateRequest(projectList) {
                    KeepMajor = settings.Minor,
                    ConsiderRequestedMajor = settings.Major > 0
                };
                updateResult = await NuGetProvider.UpdatePackageVersionsAsync(updateRequest, ct);
            });

            var updatedPackageCount = updateResult.UpdatedPackages.Count(p => p.Updated);
            if (updatedPackageCount > 0) AnsiConsole.MarkupLine($"[lime]✓ Success:[/] A total of: [green]{updatedPackageCount}[/] packages have been updated sucessfully");
            else AnsiConsole.MarkupLine("[yellow]? Warning:[/] No packages have been updated during this operation!");

            if (updateResult.Failed) {
                AnsiConsole.MarkupLine("[red]✗ Problem:[/] One or more of your packages could not be updated!");
                NuGetCommandHandler.PrintUpdateFailedPackageList(updateResult);
                return await Task.FromResult(-1);
            }

            var updatedPackages = NuGetProvider.GetNuGetPackageProjects(settings.Path);
            updatedPackages = await NuGetCommandHandler.GetPackagesAsync(settings, nugetProvider, updatedPackages, minimumFrameworkVersion, ct);

            if (settings.Silent || AnsiConsole.Confirm($"[blue]? Confirm:[/] Your [yellow]packages[/] has been updated [lime]sucessfully![/] Would you like to run [blue]restore[/] to [blue]install[/] the new versions?")) {
                var restoreResult = await ExecuteNuGetRestoreCommandAsync(settings, ct);
                if (restoreResult != -1) {
                    NuGetCommandHandler.PrintPackagesTable(settings, updatedPackages);
                    await CheckForVulnerabilities(updatedPackages, ct);
                }
                return await Task.FromResult(restoreResult);
            }

            NuGetCommandHandler.PrintPackagesTable(settings, updatedPackages);
            return await Task.FromResult(0);
        }

        public static async Task CheckForVulnerabilities(NuGetPackages projectList, CancellationToken ct = default) {
            await AnsiConsole.Status().Spinner(Spinner.Known.Moon).SpinnerStyle(Style.Parse("yellow")).StartAsync("Checking for vulnerabilities...", async ctx => {
                Extensions.ReasonableWait();
                NuGetCommandHandler.PrintVulnerabilityReport(projectList);
            });
        }

        public static async Task<int> ExecuteNuGetRestoreCommandAsync(Settings settings, CancellationToken ct = default) {
            var restoreFile = GetProjectFileToRestore(settings);
            if (string.IsNullOrEmpty(restoreFile)) {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] The specified path does not exist. Please provide a valid path to a csproj, sln or slnx file or it's containing folder.");
                return -1;
            }
            else if (restoreFile.Contains("cancelled", StringComparison.InvariantCultureIgnoreCase)) {
                AnsiConsole.MarkupLine("[yellow]? Warning:[/] Restore command cancelled!");
                return -1;
            }

            int restoreResult = 0;
            var standardOutputLines = new List<string>();
            var failedOutputLines = new List<string>();
            var warningOutputLines = new List<string>();

            void onDataOutput(string data) {
                var text = $"{data.Replace("[", "(").Replace("]", ")")}";
                if (text.IsNuGetWarningOutput()) warningOutputLines.Add(text.FormatNuGetWarningOutput(restoreFile));
                else if (text.IsNuGetRestoredOutput()) standardOutputLines.Add(text.FormatNuGetRestoreOutput());
                else if (text.IsNuGetDeterminingOutput()) standardOutputLines.Add(text.FormatNuGetDeterminingOutput());
                else if (text.IsNuGetErrorOutput()) standardOutputLines.Add(text.FormatNuGetErrorOutput(restoreFile));
                else if (text.IsNuGetErrorAnnotationOutput()) standardOutputLines.Add(text.FormatNuGetErrorAnnotationOutput());
                else standardOutputLines.Add(text);
            }

            void onErrorOutput(string data) {
                failedOutputLines.Add($"[red]{data.Replace("[", ")").Replace("]", ")")}[/]");
            }

            await AnsiConsole.Status().Spinner(Spinner.Known.Moon).SpinnerStyle(Style.Parse("Gold1")).StartAsync("Running [DarkOrange]restore[/] to upgrade your packages...", async ctx => {
                await Extensions.ReasonableWaitAsync(1000, ct);

                var restoreCommand = $"restore \"{restoreFile}\" --force --force-evaluate --ignore-failed-sources --tl:off";
                var request = new DotNetCommandRequest(restoreFile){
                    Command = restoreCommand,
                    OnDataOutput = onDataOutput,
                    OnErrorOutput = onErrorOutput,
                    LocationAction = (found, locationOrMessage) => {
                        AnsiConsole.MarkupLine(found ?
                            $"[blue]✓ Command:[/] Package manager instance found! Using: [Gray]{locationOrMessage}[/]" :
                            $"[red]✗ Problem:[/] {locationOrMessage}");
                    }
                };
                restoreResult = await NuGetProvider.RunDotNetCommandAsync(request, ct);
            });

            List<string> allOutputLines = [..failedOutputLines, ..warningOutputLines, ..standardOutputLines];
            var outputLinesText = Environment.NewLine + string.Join(Environment.NewLine, allOutputLines) + Environment.NewLine; 
            if (string.IsNullOrEmpty(outputLinesText)) { return await Task.FromResult(restoreResult); }

            var headerText = "[blue]dotnet restore[/]";
            if (allOutputLines.Any(l => l.Contains("warning", StringComparison.InvariantCultureIgnoreCase))) headerText = "[darkorange]dotnet restore[/]";
            if (allOutputLines.Any(l => l.Contains("error", StringComparison.InvariantCultureIgnoreCase))) headerText = "[red]dotnet restore[/]";
            if (allOutputLines.Any(l => l.Contains("vulnerability", StringComparison.InvariantCultureIgnoreCase))) headerText = "[red]dotnet restore[/]";
            if (allOutputLines.Any(l => l.Contains("severity", StringComparison.InvariantCultureIgnoreCase))) headerText = "[red]dotnet restore[/]";

            var panel = new Panel(outputLinesText)
                    .Header(headerText)
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("gray"));

            AnsiConsole.Write(panel);

            if (outputLinesText.Contains(": error", StringComparison.OrdinalIgnoreCase)) restoreResult = -1;

            if (restoreResult >= 0) {
                var confirmText = "[blue]? Confirm:[/] Would you like to run [blue]dotnet build[/] to verify the project compiles successfully with the restored packages?";
                if (settings.Silent || settings.Build || AnsiConsole.Confirm(Environment.NewLine + confirmText + Environment.NewLine)) {
                    return await ExecuteDotNetBuildCommand(settings, restoreFile, ct);
                }
            }

            return await Task.FromResult(restoreResult);
        }

        public static async Task<int> ExecuteDotNetBuildCommand(Settings settings, string projectFile = null, CancellationToken ct = default) {
            var buildFile = projectFile ?? GetProjectFileToRestore(settings);
            if (string.IsNullOrEmpty(buildFile)) {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] The specified path does not exist. Please provide a valid path to a csproj, sln or slnx file or it's containing folder.");
                return -1;
            }
            else if (buildFile.Contains("cancelled", StringComparison.InvariantCultureIgnoreCase)) {
                AnsiConsole.MarkupLine("[yellow]? Warning:[/] Restore command cancelled!");
                return -1;
            }

            int buildResult = 0;
            var standardOutputLines = new List<string>();
            var failedOutputLines = new List<string>();
            var warningOutputLines = new List<string>();

            void onDataOutput(string data) {
                var text = $"{data.Replace("[", "(").Replace("]", ")")}";
                if (text.IsNuGetWarningOutput()) warningOutputLines.Add(text.FormatNuGetWarningOutput(buildFile));
                else if (text.IsNuGetRestoredOutput()) standardOutputLines.Add(text.FormatNuGetRestoreOutput());
                else if (text.IsNuGetDeterminingOutput()) standardOutputLines.Add(text.FormatNuGetDeterminingOutput());
                else if (text.IsNuGetErrorOutput()) standardOutputLines.Add(text.FormatNuGetErrorOutput(buildFile));
                else if (text.IsNuGetErrorAnnotationOutput()) standardOutputLines.Add(text.FormatNuGetErrorAnnotationOutput());
                else standardOutputLines.Add(text);
            }

            void onErrorOutput(string data) {
                failedOutputLines.Add($"[red]{data.Replace("[", ")").Replace("]", ")")}[/]");
            }

            await AnsiConsole.Status().Spinner(Spinner.Known.Moon).SpinnerStyle(Style.Parse("Gold1")).StartAsync("Running [DarkOrange]build[/] to compile your projects...", async ctx => {
                await Extensions.ReasonableWaitAsync(1000, ct);

                var restoreCommand = $"build \"{buildFile}\" --disable-build-servers --force --no-restore --nologo --tl:off";
                var request = new DotNetCommandRequest(buildFile){
                    Command = restoreCommand,
                    OnDataOutput = onDataOutput,
                    OnErrorOutput = onErrorOutput,
                    LocationAction = (found, locationOrMessage) => {
                        AnsiConsole.MarkupLine(found ?
                            $"[blue]✓ Command:[/] Package manager instance found! Using: [Gray]{locationOrMessage}[/]" :
                            $"[red]✗ Problem:[/] {locationOrMessage}");
                    }
                };
                buildResult = await NuGetProvider.RunDotNetCommandAsync(request, ct);
            });

            List<string> allOutputLines = [..failedOutputLines, ..warningOutputLines, ..standardOutputLines];
            var outputLinesText = Environment.NewLine + string.Join(Environment.NewLine, allOutputLines);
            if (string.IsNullOrEmpty(outputLinesText)) { return await Task.FromResult(buildResult); }

            var headerText = "[blue]dotnet build[/]";
            if (allOutputLines.Any(l => l.Contains("warning", StringComparison.InvariantCultureIgnoreCase))) headerText = "[darkorange]dotnet build[/]";
            if (allOutputLines.Any(l => l.Contains("error", StringComparison.InvariantCultureIgnoreCase))) headerText = "[red]dotnet build[/]";
            if (allOutputLines.Any(l => l.Contains("vulnerability", StringComparison.InvariantCultureIgnoreCase))) headerText = "[red]dotnet build[/]";
            if (allOutputLines.Any(l => l.Contains("severity", StringComparison.InvariantCultureIgnoreCase))) headerText = "[red]dotnet build[/]";

            var panel = new Panel(outputLinesText)
                    .Header(headerText)
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("gray"));

            AnsiConsole.Write(panel);

            return await Task.FromResult(buildResult);
        }

        public static string GetProjectFileToRestore(Settings settings) {

            var projectFile = new FileInfo(settings.Path);
            if (projectFile.Exists) {
                return projectFile.FullName;
            }

            var solutionFolderPath = settings.Path ?? Directory.GetCurrentDirectory();
            var solutionFolder = new DirectoryInfo(solutionFolderPath);
            if (solutionFolder.Exists) {

                var csProjFiles = solutionFolder.GetFiles("*.csproj", SearchOption.AllDirectories);
                var slnFiles = solutionFolder.GetFiles("*.sln", SearchOption.AllDirectories);
                var slnxFiles = solutionFolder.GetFiles("*.slnx", SearchOption.AllDirectories);

                List<FileInfo> solutionFiles = [..slnFiles, ..slnxFiles];
                if (solutionFiles.Count == 1) {
                    return solutionFiles[0].FullName;
                }
                else {
                    List<FileInfo>  allProjectFiles = [..slnxFiles, ..slnFiles, ..csProjFiles];
                    Dictionary<string, string> choiceList = allProjectFiles.ToDictionary(f => f.FullName, f => f.Name);

                    if (settings.Silent) return allProjectFiles.FirstOrDefault()?.FullName;

                    var cancelOptionKey = "Cancel Restore Operation";
                    var selectedFile = AnsiConsole.Prompt(
                            new SelectionPrompt<string>()
                                .Title("[blue]? Confirm:[/] [yellow]Multiple projects[/] have been found. Please, select which project would you like to execute the [lime]restore[/] command:")
                                .AddChoices(choiceList.Keys.Concat([cancelOptionKey]))
                                .UseConverter(f => choiceList.TryGetValue(f, out string value) ? value : f)
                        );

                    if (string.IsNullOrEmpty(selectedFile) || selectedFile == cancelOptionKey) {
                        return "cancelled";
                    }

                    return selectedFile;
                }
            }

            return null;
        }
    }
}

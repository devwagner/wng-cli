using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using wng.Model.Npm;
using wng.Model.Shared;
using wng.Providers;

namespace wng.Commands.Npm {
    internal class NpmCommand : AsyncCommand<NpmCommand.Settings> {

        public class Settings : CommandSettings, IPackageCommandSettings {

            public static readonly string CommandKey = "npm";
            public static readonly string CommandDescription = "Manage and evaluate npm package versions";

            [CommandArgument(0, "[path]")]
            [Description("(Optional) [Gray]The path to the package.json file or the folder it resides. If not specified, the current working directory will be used.[/]")]
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
            [Description("[Gray]Run the [DarkOrange]npm install[/] command even if you don't provide the [Blue]--update[/] argument. This is useful when checking for vulnerabilities when the package-lock.json still doesn't exist.[/]")]
            public bool Install { get; set; }

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

            [CommandOption("--update")]
            [Description("[Gray]Update the packages to desired version. Latest version by default, lastest of the current Major when the [blue]--minor[/] option is specified or latest requested version when the [blue]--major <n>[/] option is specified.[/]")]
            [DefaultValue(false)]
            public bool Update { get; init; }

            [CommandOption("--silent")]
            [Description("[Gray]When using the [Blue]--update[/] argument, we won't ask for confirmation to continue or to install the packages. Both operations will be executed.[/]")]
            [DefaultValue(false)]
            public bool Silent { get; set; }

            [CommandOption("--debug")]
            [Description("[Gray]Only to be used during [Blue]development[/] to include additional columns in the list for debugging purposes.[/]")]
            [DefaultValue(false)]
            public bool Debug { get; set; }

            public List<string> InclusivePackageList => [.. PackageNames?.Split(',', StringSplitOptions.RemoveEmptyEntries)];
            public List<string> IgnorePackageList => [.. IgnorePackageNames?.Split(',', StringSplitOptions.RemoveEmptyEntries)];
        }

        public static void Register(IConfigurator config) {
            config.AddCommand<NpmCommand>(Settings.CommandKey)
                .WithDescription(Settings.CommandDescription)
                .WithExample("npm")
                .WithExample("npm", "--update")
                .WithExample("npm", "--pre")
                .WithExample("npm", "--minor")
                .WithExample("npm", "--minor", "--update")
                .WithExample("npm", "--packages", """@date-fns,typescript""")
                .WithExample("npm", "--packages", """typescript""", "--major", "4")
                .WithExample("npm", "\"C:\\project\\app\\package.json\"", "--packages", "\"tslib,typescript\"", "--update");
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct) {
            App.PrintAppWelcome();

            if (settings.Update) AnsiConsole.MarkupLine("[blue]֍ Command: Upgrade project npm package versions[/]");
            else AnsiConsole.MarkupLine("[blue]֍ Command: Evaluate project referenced npm package versions[/]");

            NpmPackages projectList = null;
            AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("yellow")).Start("Locating package.json...", ctx => {
                projectList = NpmProvider.GetNpmPackageProjects(settings.Path);
            });

            if (projectList == null || projectList.Projects.Count == 0) {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] package.json file not found. Please make sure you are in the right directory or specify a valid path using the [blue]--path[/] option");
                return await Task.FromResult(-1);
            }
            AnsiConsole.MarkupLine($"[green]✓ Success:[/] File: [yellow]./{projectList.PrimaryPackageJson.FileName}[/] loaded!");

            using var npmProvider = new NpmProvider();
            projectList = await NpmCommandHandler.GetPackagesAsync(settings, npmProvider, settings.Path, ct);
            if (projectList == null || projectList.AllPackages.Count == 0) {
                AnsiConsole.MarkupLine("[DarkOrange]? Warning:[/] No packages found in the package.json file");
                return await Task.FromResult(-1);
            }
            AnsiConsole.MarkupLine($"[green]✓ Success:[/] A total of [CadetBlue]{projectList.AllPackages.Count}[/] where found!");

            if (settings.Update) return await ExecutePackageUpdateCommandAsync(settings, projectList, npmProvider, ct);
            return await ExecutePackageListCommand(settings, projectList, ct);
        }

        public static async Task<int> ExecutePackageListCommand(Settings settings, NpmPackages projectList, CancellationToken ct = default) {
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

            if (settings.Install) commandResult = await ExecutePackageInstallCommandAsync(projectList.PrimaryPackageJson.FilePath, ct);
            NpmCommandHandler.PrintPackagesTable(settings, projectList);
            await CheckForVulnerabilities(projectList, ct);

            return await Task.FromResult(commandResult);
        }

        public static async Task<int> ExecutePackageUpdateCommandAsync(Settings settings, NpmPackages projectList, NpmProvider npmProvider, CancellationToken ct = default) {
            var packagesToUpdateCount = projectList.AllPackages.Count(p => p.CurrentVersion is not null && p.HasVersionMismatch && !p.HasFailed);
            var primaryPackageJson = projectList.PrimaryPackageJson;
            AnsiConsole.MarkupLine($"[green]✓ Success:[/] Analysis complete for [CadetBlue]{projectList.AllPackages.Count}[/] packages");

            if (packagesToUpdateCount == 0) {
                AnsiConsole.MarkupLine($"[DarkOrange]? Warning:[/] No changes are needed for any of your packages at this time");

                if (settings.Silent || AnsiConsole.Confirm($"[blue]? Confirm:[/] Would you like to use [blue]npm[/] to [blue]install[/] refresh your packages?")) {
                    var npmInstallResult = await ExecutePackageInstallCommandAsync(primaryPackageJson.FilePath, ct);
                    if (npmInstallResult != -1) {
                        NpmCommandHandler.PrintPackagesTable(settings, projectList);
                        await CheckForVulnerabilities(projectList, ct);
                    }
                    return await Task.FromResult(npmInstallResult);
                }

                return await Task.FromResult(-1);
            }

            if (!settings.Silent && !AnsiConsole.Confirm($"[blue]? Confirm:[/] We have found [darkorange]{packagesToUpdateCount}[/] packages to update. Would you like to continue?")) {
                AnsiConsole.MarkupLine("[purple]! Warning:[/] Operation cancelled!");
                return await Task.FromResult(0);
            }

            NpmPackageUpdateResult updateResult = null;
            await AnsiConsole.Status().Spinner(Spinner.Known.Moon).SpinnerStyle(Style.Parse("yellow")).StartAsync("Updating npm packages...", async ctx => {
                Extensions.ReasonableWait(1000);
                var updateRequest = new NpmPackageUpdateRequest(projectList) {
                    KeepMajor = settings.Minor,
                    ConsiderRequestedMajor = settings.Major > 0
                };
                updateResult = await NpmProvider.UpdatePackageVersionsAsync(updateRequest, ct);
            });

            var updatedPackageCount = updateResult.UpdatedPackages.Count(p => p.Updated);
            if (updatedPackageCount > 0) AnsiConsole.MarkupLine($"[lime]✓ Success:[/] A total of: [green]{updatedPackageCount}[/] packages have been updated sucessfully");
            else AnsiConsole.MarkupLine("[yellow]? Warning:[/] No packages have been updated during this operation!");

            if (updateResult.Failed) {
                AnsiConsole.MarkupLine("[red]✗ Problem:[/] One or more of your packages could not be updated!");
                NpmCommandHandler.PrintUpdateFailedPackageList(updateResult);
                return await Task.FromResult(-1);
            }

            var updatedPackages = await NpmCommandHandler.GetPackagesAsync(settings, npmProvider, settings.Path, ct);

            if (!updatedPackages.PrimaryPackageJson.FileName.Equals("package.json", StringComparison.InvariantCultureIgnoreCase)) {
                NpmCommandHandler.PrintPackagesTable(settings, updatedPackages);
                return await Task.FromResult(0);
            }

            if (settings.Silent || AnsiConsole.Confirm($"[blue]? Confirm:[/] Your [yellow]packages[/] has been updated [lime]sucessfully![/] Would you like to use [blue]npm[/] to [blue]install[/] the new versions?")) {
                var npmInstallResult = await ExecutePackageInstallCommandAsync(updatedPackages.PrimaryPackageJson.ProjectPath, ct);
                if (npmInstallResult != -1) {
                    NpmCommandHandler.PrintPackagesTable(settings, updatedPackages);
                    await CheckForVulnerabilities(projectList, ct);
                }
                return await Task.FromResult(npmInstallResult);
            }

            return await Task.FromResult(0);
        }

        public static async Task CheckForVulnerabilities(NpmPackages projectList, CancellationToken ct = default) {
            await AnsiConsole.Status().Spinner(Spinner.Known.Moon).SpinnerStyle(Style.Parse("yellow")).StartAsync("Checking for vulnerabilities...", async ctx => {
                Extensions.ReasonableWait();
                await NpmCommandHandler.ProcessVulnerabilitiesAsync(projectList, ct);
            });
        }

        public static async Task<int> ExecutePackageInstallCommandAsync(string packageJsonFilePath, CancellationToken ct = default) {
            int npmResult = 0;
            var standardOutputLines = new List<string>();
            var failedOutputLines = new List<string>();

            void onDataOutput(string data) {
                if (data.HasCommandAnnotation()) data = data.FormatCommandAnnotation();
                if (data.IsNpmInstallOutput()) standardOutputLines.Add(data.FormatNpmInstallOutput());
                else if (data.IsNpmWarningOutput()) standardOutputLines.Add(data.FormatNpmWarningOutput());
                else if (data.IsNpmFundingOutput()) standardOutputLines.Add(data.FormatNpmFundingOutput());
                else if (data.IsNpmCriticalSeverityOutput()) standardOutputLines.Add(data.FormatNpmCriticalSeverityOutput());
                else standardOutputLines.Add($"{data}");
            }

            void onErrorOutput(string data) {
                if (data.HasCommandAnnotation()) data = data.FormatCommandAnnotation();
                if (data.IsNpmWarningOutput()) failedOutputLines.Add(data.FormatNpmWarningOutput());
                else if (data.IsNpmErrorOutput()) failedOutputLines.Add(data.FormatNpmErrorOutput());
                else failedOutputLines.Add($"[red]{data}[/]");
            }

            await AnsiConsole.Status().Spinner(Spinner.Known.Moon).SpinnerStyle(Style.Parse("Gold1")).StartAsync("Running [DarkOrange]npm install[/] to upgrade your packages...", async ctx => {
                var npmCommandRequest = new NpmCommandRequest {
                    PackageJsonFilePath = packageJsonFilePath,
                    Command = "install",
                    OnDataOutput = onDataOutput,
                    OnErrorOutput = onErrorOutput,
                    NpmLocationAction = (found, npmLocationOrMessage) => {
                        AnsiConsole.MarkupLine(found ?
                            $"[blue]✓ Command:[/] Package manager instance found! Using: [Gray]{npmLocationOrMessage}[/]" :
                            $"[red]✗ Problem:[/] {npmLocationOrMessage}");
                    }
                };
                npmResult = await NpmProvider.RunNpmCommandAsync(npmCommandRequest, ct);
            });

            var allOutputLines = failedOutputLines.Concat(standardOutputLines).ToList();
            var outputLinesText = string.Join(Environment.NewLine, allOutputLines);
            if (string.IsNullOrEmpty(outputLinesText)) { return await Task.FromResult(npmResult); }

            var headerText = "[blue]npm install[/]";
            if (allOutputLines.Any(l => l.Contains("npm warn", StringComparison.InvariantCultureIgnoreCase))) headerText = "[darkorange]npm install[/]";
            if (allOutputLines.Any(l => l.Contains("npm error", StringComparison.InvariantCultureIgnoreCase))) headerText = "[red]npm install[/]";
            if (allOutputLines.Any(l => l.Contains("vulnerability", StringComparison.InvariantCultureIgnoreCase))) headerText = "[red]npm install[/]";
            if (allOutputLines.Any(l => l.Contains("critical severity", StringComparison.InvariantCultureIgnoreCase))) headerText = "[red]npm install[/]";

            var panel = new Panel(outputLinesText)
                    .Header(headerText)
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("gray"));

            AnsiConsole.WriteLine("");
            AnsiConsole.Write(panel);

            return await Task.FromResult(npmResult);
        }
    }
}

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using wng.Model;
using wng.Model.Dotnet;
using wng.Model.Shared;
using wng.Providers;

namespace wng.Commands.Dotnet {
    internal class DotnetCommand : AsyncCommand<DotnetCommand.Settings> {

        public class Settings : CommandSettings, IFrameworkCommand {

            public static readonly string CommandKey = "dotnet";
            public static readonly string CommandDescription = "Manage and evaluate Dotnet Framework versions";

            [CommandArgument(0, "[path]")]
            [Description("(Optional) [Gray]The path to the csproj or the solution folder it resides. If not specified, the current working directory will be used.[/]")]
            public string Path { get; init; } = Directory.GetCurrentDirectory();

            [CommandOption("-p|--projects <projects>")]
            [Description("""[Gray]A comma-separated list of names or partial names to filter the projects during analysis. Ex. "project1,project2,project3".[/]""")]
            [DefaultValue(null)]
            public string ProjectNames { get; init; } = string.Empty;

            [CommandOption("-i|--install <version>", isRequired: false)]
            [Description("[Gray]Use this option to install a new version of the [green]dotnet[/] Framework. If no version is defined, you will be able to selection from a list.[/]")]
            [DefaultValue("none")]
            public string Install { get; set; } = "none";

            [CommandOption("-u|--update <version>", isRequired: false)]
            [Description("[Gray]Update the [green]dotnet[/] version of the project(s). If you define [blue]latest[/], the latest version will be used. If no version is defined, you will be able to selection from a list.[/]")]
            [DefaultValue("none")]
            public string Update { get; init; } = "none";

            [CommandOption("--sdk")]
            [Description("[Gray]List the available [pink1]dotnet SDK Releases[/]  and compare with the installed versions.[/]")]
            public bool Sdk { get; set; }

            [CommandOption("--runtime")]
            [Description("[Gray]List the available [pink1]dotnet Runtime Releases[/] and compare with the installed versions.[/]")]
            public bool Runtime { get; set; }

            [CommandOption("--silent")]
            [Description("[Gray]We won't ask for confirmation to continue, update or install. All operations will be executed.[/]")]
            [DefaultValue(false)]
            public bool Silent { get; set; }

            [CommandOption("--debug")]
            [Description("[Gray]Only to be used during [Blue]development[/] to include additional columns in the list for debugging purposes.[/]")]
            [DefaultValue(false)]
            public bool Debug { get; set; }
        }

        public static void Register(IConfigurator config) {
            config.AddCommand<DotnetCommand>(Settings.CommandKey)
            .WithDescription(Settings.CommandDescription)
            .WithExample("dotnet")
            .WithExample("dotnet", "--update", "10.0")
            .WithExample("dotnet", "--update", "latest")
            .WithExample("dotnet", "--sdk", "--install", "latest")
            .WithExample("dotnet", "--runtime", "--install", "10.0")
            .WithExample("dotnet", "\"C:\\MyProject\"", "--projects", "\"MyProject.Core,MyProject.Web\"\"", "--update", "latest");
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct) {
            App.PrintAppWelcome();

            // Validate the command options
            if (settings.Sdk == true && settings.Runtime == true) {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] You cannot use both --sdk and --runtime options at the same time. Please choose one of them.");
                return await Task.FromResult(-1);
            }
            if ((settings.Sdk || settings.Runtime) && (settings.Update != "none")) {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] You cannot use --sdk or --runtime options together with --update. Update is only used to update your dotnet projects. Please choose one of them.");
                return await Task.FromResult(-1);
            }
            if ((settings.Sdk || settings.Runtime) && (!string.IsNullOrEmpty(settings.ProjectNames))) {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] You cannot use --sdk or --runtime options together with --projects. Project filtering is only used when updating your dotnet projects. Please choose one of them.");
                return await Task.FromResult(-1);
            }
            if (!settings.Sdk && !settings.Runtime && settings.Install != "none") {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] You must specify either --sdk or --runtime option when using --install to install a new dotnet version.");
                return await Task.FromResult(-1);
            }

            // Set default install command to latest when no version is specified
            if (settings.Install == "__default_command") { settings.Install = "latest"; }

            // Display the command being executed
            if (settings.Install != "none") AnsiConsole.MarkupLine("[blue]֍ Command: Install dotnet version[/]");
            else if (settings.Update != "none") AnsiConsole.MarkupLine("[blue]֍ Command: Update dotnet version in project(s)[/]");
            else if (settings.Sdk == true) AnsiConsole.MarkupLine("[blue]֍ Command: Evaluate avaliable dotnet SDK versions[/]");
            else if (settings.Runtime == true) AnsiConsole.MarkupLine("[blue]֍ Command: Evaluate avaliable dotnet Runtime versions[/]");

            // Initialize the necessary providers
            using var dotnetProvider = new DotnetProvider(settings.Debug);
            using var nuGetProvider = new NuGetProvider(settings.Debug);

            // Evaluate dotnet versions
            if ((settings.Sdk || settings.Runtime) && settings.Install == "none") {
                await ListDotnetVersionsAsync(settings, dotnetProvider, true, ct);
                return await Task.FromResult(0);
            }
            else if (settings.Install != "none") {
                return await InstallDotnetVersionsAsync(settings, dotnetProvider, ct);
            }
            else if (settings.Update != "none") {
                AnsiConsole.MarkupLine("[blue]? <TODO>: [/] Implement [yellow]dotnet version project update[/] command to enable this feature!");
                return await Task.FromResult(0);
            }

            AnsiConsole.MarkupLine("[blue]? <TODO>: [/] Implement [yellow]dotnet version project analysis table[/] command to enable this feature!");
            return await Task.FromResult(0);
        }

        public static async Task<int> InstallDotnetVersionsAsync(Settings settings, DotnetProvider dotnetProvider, CancellationToken ct = default) {
            var dotnetVersions = await ListDotnetVersionsAsync(settings, dotnetProvider, false, ct);

            var versionToInstall = settings.Install;
            var channelToInstall = versionToInstall;
            DotnetFrameworkVersionRelease dotNetVersionToInstall;


            DotnetFrameworkVersionRelease sdkVersion = null;

            if (versionToInstall == "latest") {
                var dotnetVersionRelease = dotnetVersions.NetCore.Versions.Where(v => v.LatestSdk != null).OrderByDescending(v => v.LatestSdk.Version).FirstOrDefault();
                channelToInstall = dotnetVersionRelease?.ChannelVersion.ToString();
                sdkVersion = dotnetVersionRelease?.LatestSdk;
            }
            else {
                var parsedVersionRef = new PackageVersion(versionToInstall);
                var parsedVersion = $"{parsedVersionRef.Major}.{parsedVersionRef.Minor ?? "0"}";
                sdkVersion = dotnetVersions.NetCore.Versions.Where(v => v.LatestSdk != null && v.ChannelVersion == parsedVersion).FirstOrDefault()?.LatestSdk;
            }

            if (sdkVersion != null) {
                versionToInstall = sdkVersion.Version.ToString();
                dotNetVersionToInstall = sdkVersion;
                //AnsiConsole.MarkupLine($"[blue]֍ Command: Install latest dotnet SDK version [CadetBlue]{versionToInstall}[/][/]");
            }
            else {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] No dotnet SDK versions found to install.");
                return await Task.FromResult(-1);
            }

            if (sdkVersion.IsUpToDate) {
                AnsiConsole.MarkupLine($"[DarkOrange]? Warning:[/] The latest dotnet [Cyan]SDK version[/] [CadetBlue]{versionToInstall}[/] for channel [CadetBlue]{channelToInstall}[/] is already installed and up to date.");
                DotnetCommandHandler.PrintVersionsTable(settings, dotnetVersions.NetCore.Versions);
                return await Task.FromResult(0);
            }

            AnsiConsole.MarkupLine($"[green]✓ Success:[/] Latest dotnet SDK version for the channel was found: [CadetBlue]{versionToInstall}[/]");

            var result = await DotnetProvider.InstallDotnetVersion(sdkVersion, settings.Runtime, ct);

            if (result?.Count > 0) {
                var panel = new Panel(string.Join(Environment.NewLine, result))
                    .Header("dotnet install")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("gray"));

                AnsiConsole.Write("");
                AnsiConsole.Write(panel);
            }

            return await Task.FromResult(0);
        }


        public static async Task<DotnetFrameworkVersions> ListDotnetVersionsAsync(Settings settings, DotnetProvider dotnetProvider, bool print = false, CancellationToken ct = default) {
            DotnetFrameworkVersions dotnetVersions = null;

            await AnsiConsole.Status().Spinner(Spinner.Known.Moon).SpinnerStyle(Style.Parse("Gold1")).StartAsync("Evaluating [blue]dotnet[/] versions...", async ctx => {
                await Extensions.ReasonableWaitAsync(1000, ct);
                try {
                    dotnetVersions = await dotnetProvider.GetDotNetVersionsAsync(ct);
                }
                catch (Exception ex) {
                    AnsiConsole.WriteException(ex, new ExceptionSettings {
                        Format = ExceptionFormats.ShortenEverything,
                        Style = new ExceptionStyle {
                            Exception = new Style(Color.Grey),
                            Message = new Style(Color.White),
                            Method = new Style(Color.Red),
                            Path = new Style(Color.Yellow),
                            LineNumber = new Style(Color.Blue),
                        }
                    });
                }
            });

            if (dotnetVersions == null || dotnetVersions.Failed) {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] Failed to retrieve dotnet versions. {dotnetVersions?.Message}");
                return null;
            }

            if (!(dotnetVersions.NetCore?.Versions?.Count > 0)) {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] No .NET Core versions found.");
                return null;
            }

            var dotnetVersionsFound = dotnetVersions.NetCore.Versions.Count;
            AnsiConsole.MarkupLine($"[green]✓ Success:[/] A total of [CadetBlue]{dotnetVersionsFound}[/] dotnet core versions where found!");

            if (print) {

                if (settings.Sdk) {
                    var dotnetVersionsSdkOutOfDateCount = dotnetVersions.NetCore.Versions.Count(v => v.LatestSdk != null && v.LatestSdk.IsInstalled && !v.LatestSdk.IsUpToDate);
                    if (dotnetVersionsSdkOutOfDateCount > 0) {
                        AnsiConsole.MarkupLine($"[DarkOrange]? Warning:[/] You have a total of [CadetBlue]{dotnetVersionsSdkOutOfDateCount}[/] dotnet core SDK versions out of date.");
                        AnsiConsole.MarkupLine($"           Use [Blue]wng dotnet --sdk --install[/] [Cyan]<channel>[/] command to upgrade");
                    }
                    else {
                        var installedVersions = dotnetVersions.NetCore.Versions.Count(v => v.LatestSdk != null && v.LatestSdk.IsInstalled);
                        AnsiConsole.MarkupLine($"[green]✓ Success:[/] Total of [CadetBlue]{installedVersions}[/] installed dotnet core SDK versions are up to date!");
                    }
                }

                if (settings.Runtime) {
                    var dotnetVersionsRuntimeOutOfDateCount = dotnetVersions.NetCore.Versions.Count(v => v.LatestRuntime != null && v.LatestRuntime.IsInstalled && !v.LatestRuntime.IsUpToDate);
                    if (dotnetVersionsRuntimeOutOfDateCount > 0) {
                        AnsiConsole.MarkupLine($"[DarkOrange]? Warning:[/] You have a total of [CadetBlue]{dotnetVersionsRuntimeOutOfDateCount}[/] dotnet core Runtime versions out of date.");
                        AnsiConsole.MarkupLine($"           Use [Blue]wng dotnet --runtime --install[/] [Cyan]<channel>[/] command to upgrade");
                    }
                    else {
                        var installedVersions = dotnetVersions.NetCore.Versions.Count(v => v.LatestRuntime != null && v.LatestRuntime.IsInstalled);
                        AnsiConsole.MarkupLine($"[green]✓ Success:[/] Total of [CadetBlue]{installedVersions}[/] installed dotnet core Runtime versions are up to date!");
                    }
                }


                await AnsiConsole.Status().Spinner(Spinner.Known.Moon).SpinnerStyle(Style.Parse("Gold1")).StartAsync("Processing all [blue]dotnet[/] versions...", async ctx => {
                    await Extensions.ReasonableWaitAsync(1000, ct);
                    DotnetCommandHandler.PrintVersionsTable(settings, dotnetVersions.NetCore.Versions);
                });
            }

            return dotnetVersions;
        }

    }
}

using Spectre.Console;
using wng.Model;
using wng.Model.Dotnet;
using wng.Providers;

namespace wng.Commands.NuGet {
    internal sealed class NuGetCommandHandler {

        public static void PrintPackagesTable(NuGetCommand.Settings settings, NuGetPackages projectList) {
            var hasMultipleProjects = projectList.Projects.Count > 1;
            foreach (var project in projectList.Projects) {
                var packages = project.Packages.ToList();
                NuGetPackageTable.Render(settings, packages, projectList.AllPackages, "PRJ", hasMultipleProjects);
                PrintFetchFailedPackagesTable(projectList);
            }
        }

        public static void PrintFetchFailedPackagesTable(NuGetPackages projectList) {
            var failedPackages = projectList.AllPackages.Where(p => p.HasFailed).ToList();
            if (failedPackages.Count > 0) {

                var failedPackagesTable = new Table().RoundedBorder().BorderColor(Color.Grey);
                failedPackagesTable.AddColumn("Status", col => col.Centered());
                failedPackagesTable.AddColumn("Name", col => col.LeftAligned());
                failedPackagesTable.AddColumn("Message", col => col.LeftAligned());

                foreach (var package in failedPackages) {
                    var statusColumn = new Markup("[red]✗[/]");
                    var nameColumn = new Text(package.Name);
                    var messageColumn = new Text(package.FailureMessage ?? "An error ocurred when trying to fetch the information for this package");
                    failedPackagesTable.AddRow(statusColumn, nameColumn, messageColumn);
                }
                AnsiConsole.Write(failedPackagesTable);
            }
        }

        public static void PrintUpdateFailedPackageList(NuGetPackageUpdateResult updateResult) {
            var failedPackages = updateResult.UpdatedPackages.Where(p => p.Failed).ToList();
            if (failedPackages.Count > 0) {

                var failedPackagesTable = new Table().RoundedBorder().BorderColor(Color.Grey);
                failedPackagesTable.AddColumn("Status", col => col.Centered());
                failedPackagesTable.AddColumn("Name", col => col.LeftAligned());
                failedPackagesTable.AddColumn("Message", col => col.LeftAligned());

                foreach (var updatedPackage in failedPackages) {
                    var statusColumn = new Markup("[red]✗[/]");
                    var nameColumn = new Text(updatedPackage.Package.Name);
                    var messageColumn = new Text(updatedPackage.Message ?? "An error ocurred when trying to update this package");
                    failedPackagesTable.AddRow(statusColumn, nameColumn, messageColumn);
                }

                AnsiConsole.Write(failedPackagesTable);
            }
        }

        public static void PrintVulnerabilityReport(NuGetPackages solution) {
            if (solution == null || !(solution.Projects?.Count > 0)) return;

            var vulnerableProjects = solution.Projects.Where(p => p.Packages.Any(p => p.CurrentVersion?.Vulnerabilities?.Count > 0));
            var vulnerabilities = vulnerableProjects.SelectMany(p => p.Packages.Where(pk => pk.CurrentVersion?.Vulnerabilities?.Count > 0).Select(pk =>
                new KeyValuePair<string, NuGetPackage>(p.Name, pk)
            ));

            //AnsiConsole.MarkupLine("[green]No vulnerabilities found.[/]");
            if (!(vulnerabilities.Any())) return;

            var table = new Table().RoundedBorder().BorderColor(Color.Grey);
            table.AddColumn("DEP", col => col.Centered());
            table.AddColumn("Project", col => col.LeftAligned().NoWrap());
            table.AddColumn("Severity", col => col.LeftAligned().NoWrap());
            table.AddColumn("Package", col => col.LeftAligned().NoWrap());
            table.AddColumn("Vulnerability Details", col => col.LeftAligned());

            foreach (var packageRef in vulnerabilities) {
                var project = packageRef.Key;
                var package = packageRef.Value;
                var version = package.CurrentVersion;

                if (!(version.Vulnerabilities.Count > 0)) continue;
                foreach (var vulnerability in version.Vulnerabilities) {

                    var vulnerabilityStyle = new Style(Color.Red, decoration: Decoration.Bold);
                    var vulnerabilityText = "Critical";

                    if (vulnerability.Severity.Equals("low", StringComparison.OrdinalIgnoreCase)) {
                        vulnerabilityStyle = new Style(Color.Yellow, decoration: Decoration.None);
                        vulnerabilityText = "Low";
                    }

                    if (vulnerability.Severity.Equals("medium", StringComparison.OrdinalIgnoreCase)) {
                        vulnerabilityStyle = new Style(Color.DarkOrange, decoration: Decoration.None);
                        vulnerabilityText = "Medium";
                    }

                    if (vulnerability.Severity.Equals("high", StringComparison.OrdinalIgnoreCase)) {
                        vulnerabilityStyle = new Style(Color.Pink1, decoration: Decoration.None);
                        vulnerabilityText = "High";
                    }


                    table.AddRow(
                        new Markup("[red bold]●[/]"),
                        new Text(project),
                        new Text($"{vulnerability.Severity} - {vulnerabilityText}".ToUpper(), vulnerabilityStyle),
                        new Text(package.Name),
                        new Text(vulnerability.AdvisoryUrl)
                    );
                }
            }

            AnsiConsole.Write(table);
        }

        public static async Task<NuGetPackages> GetPackagesAsync(NuGetCommand.Settings settings, NuGetProvider nugetProvider, NuGetPackages projectList, PackageVersion minimumFrameworkVersion, CancellationToken ct = default) {
            var result = new NuGetPackages();
            await AnsiConsole.Status().Spinner(Spinner.Known.OrangeBluePulse).SpinnerStyle(Style.Parse("blue")).StartAsync("Analysing NuGet packages...", async ctx => {
                await Extensions.ReasonableWaitAsync();
                try {
                    var solutionPath = settings.Path ?? Directory.GetCurrentDirectory();
                    var projects = await nugetProvider.GetNuGetPackagesAsync(new NuGetPackageRequest(projectList) {
                        MinimumFrameworkVersion = minimumFrameworkVersion,
                        IgnorePackages = settings.IgnorePackageList,
                        InclusivePackages = settings.InclusivePackageList,
                        IncludePreRelease = settings.IncludePreRelease,
                        PrepareAction = (p) => p.SetCommandSettings(settings.Minor, settings.Major)
                    }, ct);
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
            return result;
        }

        public static async Task<PackageVersion> GetLeastProjectVersionInSolutionAsync(string solutionPath, CancellationToken ct = default) {
            PackageVersion leastVersion = null;

            await AnsiConsole.Status().Spinner(Spinner.Known.OrangeBluePulse).SpinnerStyle(Style.Parse("blue")).StartAsync("Determining least framework version in solution...", async ctx => {
                //await Extensions.ReasonableWaitAsync();

                var solutionFolder = new DirectoryInfo(solutionPath);
                var csprojFiles = solutionFolder.GetFiles("*.csproj", SearchOption.AllDirectories);
                var foundVersions = new List<PackageVersion>();

                foreach (var csprojFile in csprojFiles) {
                    var fileContents = await File.ReadAllTextAsync(csprojFile.FullName, ct);
                    var version = fileContents.GetTargetDotNetFramework();
                    if (version != null) {
                        foundVersions.Add(version.Replace("net", "").Replace("framework", "").Replace("standard", "").Trim());
                    }
                }

                leastVersion = foundVersions.OrderBy(v => v).FirstOrDefault();
            });

            return leastVersion!;
        }

    }
}

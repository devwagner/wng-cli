using Spectre.Console;
using wng.Model.Npm;
using wng.Providers;

namespace wng.Commands.Npm {
    internal sealed class NpmCommandHandler {

        public static void PrintPackagesTable(NpmCommand.Settings settings, NpmPackages projectList) {
            var hasMultipleProjects = projectList.Projects.Count > 1;
            var allPackages = projectList.AllPackages;

            foreach (var project in projectList.Projects) {
                var depPackages = project.Dependencies.Where(r => !r.HasFailed).OrderBy(p => p.Order).ToList();
                NpmPackageTable.Render(settings, depPackages, allPackages, "PRJ", hasMultipleProjects);

                var devPackages = project.DevDependencies.Where(r => !r.HasFailed).OrderBy(p => p.Order).ToList();
                NpmPackageTable.Render(settings, devPackages, allPackages, "DEV", hasMultipleProjects);
                PrintFetchFailedPackagesTable(projectList);
            }
        }

        public static async Task ProcessVulnerabilitiesAsync(NpmPackages projectList, CancellationToken ct = default) {
            try {
                var primaryProject = projectList.PrimaryPackageJson;
                var packageLockJson = primaryProject.PackageJsonFolder.Exists ? primaryProject.PackageJsonFolder.GetFiles("package-lock.json")?.FirstOrDefault() : null;
                if (packageLockJson?.Exists != true) return;

                var auditReport = await NpmProvider.GetNpmAuditReportAsync(primaryProject.FilePath, ct);
                if (auditReport != null && auditReport.Vulnerabilities.Count > 0 && auditReport.Vulnerabilities.Any(v => v.Value.ViaDetails.Count > 0)) {
                    PrintVulnerabilityReport(auditReport);
                }
            }
            catch {
                //At this moment, we silently ignore audit report errors
                //We will catch the npm audit schema changes on the automated tests
            }
        }

        public static void PrintFetchFailedPackagesTable(NpmPackages projectList) {
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

        public static void PrintUpdateFailedPackageList(NpmPackageUpdateResult updateResult) {
            var failedPackages = updateResult.UpdatedPackages.Where(p => p.Failed).ToList();
            if (failedPackages.Count > 0) {
                AnsiConsole.WriteLine();

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

        public static void PrintVulnerabilityReport(NpmAuditResult auditReport) {
            if (auditReport == null) return;

            var vulnerabilities = auditReport.Vulnerabilities;
            if (vulnerabilities == null || vulnerabilities.Count == 0) {
                AnsiConsole.MarkupLine("[green]No vulnerabilities found.[/]");
                return;
            }

            var table = new Table().RoundedBorder().BorderColor(Color.Grey);
            table.AddColumn("DEP", col => col.Centered());
            table.AddColumn("Severity", col => col.LeftAligned().NoWrap());
            table.AddColumn("Package", col => col.LeftAligned().NoWrap());
            table.AddColumn("Range", col => col.LeftAligned().NoWrap());
            table.AddColumn("Vulnerability Details", col => col.LeftAligned());

            foreach (var vulnerability in vulnerabilities.Values) {
                if (!(vulnerability.ViaDetails.Count > 0)) continue;
                foreach (var detail in vulnerability.ViaDetails) {

                    var vulnerabilityStyle = new Style(Color.Red, decoration: Decoration.Bold);
                    if (detail.Severity.Equals("moderate", StringComparison.OrdinalIgnoreCase)) vulnerabilityStyle = new Style(Color.DarkOrange, decoration: Decoration.None);
                    if (detail.Severity.Equals("low", StringComparison.OrdinalIgnoreCase)) vulnerabilityStyle = new Style(Color.Yellow, decoration: Decoration.None);
                    if (detail.Severity.Equals("high", StringComparison.OrdinalIgnoreCase)) vulnerabilityStyle = new Style(Color.Pink1, decoration: Decoration.None);
                    if (detail.Severity.Equals("info", StringComparison.OrdinalIgnoreCase)) vulnerabilityStyle = new Style(Color.Blue, decoration: Decoration.None);

                    table.AddRow(
                        new Markup("[red bold]●[/]"),
                        new Text(detail.Severity.ToUpper(), vulnerabilityStyle),
                        new Text(detail.Name),
                        new Text(detail.Range),
                        new Text(detail.Url)
                    );
                }
            }

            AnsiConsole.Write(table);
        }

        public static async Task<NpmPackages> GetPackagesAsync(NpmCommand.Settings settings, NpmProvider npmProvider, string packageJsonFilePath, CancellationToken ct) {
            NpmPackages result = null;

            await AnsiConsole.Status().Spinner(Spinner.Known.OrangeBluePulse).SpinnerStyle(Style.Parse("blue")).StartAsync("Parsing package.json...", async ctx => {
                await Extensions.ReasonableWaitAsync();
                try {
                    result = await npmProvider.GetNmpPackagesAsync(new NpmPackageRequest(packageJsonFilePath) {
                        InclusivePackages = settings.InclusivePackageList,
                        IncludePreRelease = settings.IncludePreRelease,
                        IgnorePackages = settings.IgnorePackageList,
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
    }
}

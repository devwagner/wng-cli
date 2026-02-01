using Spectre.Console;
using Spectre.Console.Rendering;

namespace wng.Model.Shared {
    internal class PackageTable<TPackage, TSettings>(TSettings settings, List<TPackage> packages, List<TPackage> allPackages, string section, bool showProjectColumn = false, bool showFrameworkColumn = false)
        where TPackage : Package
        where TSettings : IPackageCommandSettings {

        public virtual void Render() {
            if (packages.Count > 0) {

                var packagesTable = new Table().RoundedBorder().BorderColor(Color.Grey);

                var headers = new NpmTableHeaders(settings, section, showProjectColumn, showFrameworkColumn);
                headers.Render(packagesTable);

                var rows = new NpmTableRows(settings, packages, allPackages, showProjectColumn, showFrameworkColumn);
                rows.Render(packagesTable);

                AnsiConsole.Write(packagesTable);
            }
        }

        private class NpmTableHeaders {

            public List<TableColumn> Headers { get; init; } = [];

            public NpmTableHeaders(TSettings settings, string section, bool showProjectColumn, bool showFrameworkColumn) {

                Headers.Add(new TableColumn(section) { Alignment = Justify.Center, NoWrap = true });

                if (showProjectColumn)
                    Headers.Add(new TableColumn("Project") { Alignment = Justify.Left, NoWrap = true });

                Headers.Add(new TableColumn("Name") { Alignment = Justify.Left });
                Headers.Add(new TableColumn("Version") { Alignment = Justify.Left, NoWrap = true });
                Headers.Add(new TableColumn("Version Date") { Alignment = Justify.Left, NoWrap = true });
                Headers.Add(new TableColumn("Latest") { Alignment = Justify.Left, NoWrap = true });
                Headers.Add(new TableColumn("Latest Date") { Alignment = Justify.Left, NoWrap = true });

                if (settings.Minor) {
                    Headers.Add(new TableColumn("Minor") { Alignment = Justify.Left, NoWrap = true });
                    Headers.Add(new TableColumn("Minor Date") { Alignment = Justify.Left, NoWrap = true });
                }

                if (settings.Major > 0) {
                    Headers.Add(new TableColumn("Requested") { Alignment = Justify.Left, NoWrap = true });
                    Headers.Add(new TableColumn("Requested Date") { Alignment = Justify.Left, NoWrap = true });
                }

                if (settings.ShowUrl) Headers.Add(new TableColumn("Package URL") { Alignment = Justify.Left, NoWrap = true });
                if (settings.ShowProjectUrl) Headers.Add(new TableColumn("Project URL") { Alignment = Justify.Left, NoWrap = true });
                if (settings.ShowVersionUrl) Headers.Add(new TableColumn("Version URL") { Alignment = Justify.Left, NoWrap = true });

                if(showFrameworkColumn)
                    Headers.Add(new TableColumn("Framework") { Alignment = Justify.Left, NoWrap = true });

                if (settings.Debug) {
                    Headers.Add(new TableColumn("Failed") { Alignment = Justify.Left, NoWrap = true });
                    Headers.Add(new TableColumn("Invalid") { Alignment = Justify.Left, NoWrap = true });
                    Headers.Add(new TableColumn("Mismatch") { Alignment = Justify.Left, NoWrap = true });
                    Headers.Add(new TableColumn("Latest Version") { Alignment = Justify.Left, NoWrap = true });
                    Headers.Add(new TableColumn("Latest Minor") { Alignment = Justify.Left, NoWrap = true });
                    Headers.Add(new TableColumn("Requested Match") { Alignment = Justify.Left, NoWrap = true });
                }
            }

            public void Render(Table table) => Headers.ForEach(col => table.AddColumn(col));
        }

        private class NpmTableRows(TSettings settings, List<TPackage> packages, List<TPackage> allPackages, bool showProjectColumn, bool showFrameworkColumn) {

            public List<NpmTableRow> Rows { get; init; } = [.. packages.Select(package => new NpmTableRow(settings, package, allPackages, showProjectColumn, showFrameworkColumn))];

            public void Render(Table table) => Rows.ForEach(row => table.AddRow(row.Elements));
        }

        private class NpmTableRow {

            public IEnumerable<IRenderable> Elements { get; init; }
            private readonly NpmTableColumns _columns;

            public NpmTableRow(TSettings settings, TPackage package, List<TPackage> allPackages, bool hasProjectColumn, bool hasFrameworkColumn) {
                _columns = new(settings, package, allPackages);

                IEnumerable<IRenderable> statusSection = [_columns.Status];
                if(hasProjectColumn) statusSection = [.. statusSection, _columns.Project];

                Elements = settings.Minor ?
                    [.. statusSection, _columns.Name, _columns.Version, _columns.VersionDate, _columns.LatestVersion, _columns.LatestVersionDate, _columns.MinorVersion, _columns.MinorVersionDate] :
                    [.. statusSection, _columns.Name, _columns.Version, _columns.VersionDate, _columns.LatestVersion, _columns.LatestVersionDate];

                if (settings.Major > 0) Elements = [.. Elements, _columns.RequestedVersion, _columns.RequestedVersionDate];
                if (settings.ShowUrl) Elements = [.. Elements, _columns.PackageUrl];
                if (settings.ShowProjectUrl) Elements = [.. Elements, _columns.ProjectUrl];
                if (settings.ShowVersionUrl) Elements = [.. Elements, _columns.VersionUrl];
                if(hasFrameworkColumn) Elements = [.. Elements, _columns.Framework];

                if (settings.Debug) {
                    Elements = [.. Elements,
                        _columns.HasFailed,
                        _columns.IsCurrentVersionInvalid,
                        _columns.HasVersionMismatch,
                        _columns.IsLatestVersion,
                        _columns.IsLatestMinorVersion,
                        _columns.IsRequestedVersionMatch
                    ];
                }
            }
        }

        public class NpmTableColumns(TSettings settings, TPackage package, List<TPackage> allPackages) {

            private readonly TSettings _settings = settings;
            private readonly TPackage _package = package;
            private readonly int _projectMaxLength = allPackages.Max(p => p.ProjectName.Length);
            private readonly int _nameMaxLength = allPackages.Max(p => p.Name.Length);
            private readonly int _versionMaxLength = allPackages.Max(p => p.CurrentVersion?.Version?.Length ?? 0);
            private readonly int _latestVersionMaxLength = allPackages.Max(p => p.LatestVersion?.Version?.Length ?? 0);
            private readonly int _minorVersionMaxLength = allPackages.Max(p => p.LatestMinorVersion?.Version?.Length ?? 0);
            private readonly int _requestVersionMaxLength = allPackages.Max(p => p.RequestedVersion?.Version?.Length ?? 0);

            public IRenderable Status {
                get {
                    var statusColumn = !_package.HasVersionMismatch ? new Markup("[Lime bold]✓[/]") : new Markup("[yellow bold]●[/]");
                    if (_package.IsCurrentVersionInvalid || !(_package.LatestVersion?.Version?.Length > 0)) { statusColumn = new Markup("[red bold]✗[/]"); }
                    return statusColumn;
                }
            }

            public IRenderable Project {
                get {
                    var projectColumn = new Text(_package.ProjectName.PadRight(length: _projectMaxLength, removeTextInBrackets: true));
                    return projectColumn;
                }
            }

            public IRenderable Name {
                get {
                    var nameColor = !_package.HasVersionMismatch ? Color.Default : Color.Orange1;
                    if (_package.IsCurrentVersionInvalid || !(_package.LatestVersion?.Version?.Length > 0)) nameColor = Color.Red;

                    //TODO: Not supported yet
                    //var nameText = $"[link={package.ProjectUrl}]{package.Name}[/]";
                    var nameText = _package.Name;
                    var nameColumn = new Text($"{nameText.PadRight(length: _nameMaxLength, removeTextInBrackets: true)}", new Style(foreground: nameColor));
                    return nameColumn;
                }
            }

            public IRenderable Version {
                get {
                    var versionColor = _package.IsCurrentVersionInvalid ? Color.Red : Color.LightSkyBlue1;
                    var versionColumn = new Text(_package.CurrentVersion.ParsedVersion.PadRight(length: _versionMaxLength, removeTextInBrackets: true), new Style(foreground: versionColor));
                    return versionColumn;
                }
            }

            public IRenderable VersionDate {
                get {
                    var versionDateColumn = new Markup(FormatPublishedDate(_package.CurrentVersion.PublishedAt));
                    return versionDateColumn;
                }
            }

            public IRenderable LatestVersion {
                get {
                    var latestColumnColor = !_package.HasVersionMismatch ? Color.Green : Color.Orange1;
                    if (_settings.Major > 0 || _settings.Minor) latestColumnColor = Color.Default;
                    if(!(_package.LatestVersion?.Version?.Length > 0)) latestColumnColor = Color.Red;
                    var latestColumnText = _package.LatestVersion?.Version ?? "not-found";
                    var latestColumn = new Text(latestColumnText.PadRight(length: _latestVersionMaxLength, removeTextInBrackets: true), new Style(foreground: latestColumnColor));
                    return latestColumn;
                }
            }

            public IRenderable LatestVersionDate {
                get {
                    var latestDateColumnText = _package.LatestVersion?.PublishedAt ?? default;
                    var latestDateColumn = new Markup(FormatPublishedDate(latestDateColumnText));
                    return latestDateColumn;
                }
            }

            public IRenderable MinorVersion {
                get {
                    var minorColumnColor = !_package.HasVersionMismatch ? Color.Green : Color.Orange1;
                    if (_settings.Major > 0) minorColumnColor = Color.Default;
                    if(!(_package.LatestMinorVersion?.Version?.Length > 0)) minorColumnColor = Color.Red;
                    var minorColumnText = _package.LatestMinorVersion?.Version?.PadRight(length: _minorVersionMaxLength, removeTextInBrackets: true) ?? "not-found";
                    var minorColumn = new Text(minorColumnText, new Style(foreground: minorColumnColor));
                    return minorColumn;
                }
            }

            public IRenderable MinorVersionDate {
                get {
                    var minorDateColumnText = _package.LatestMinorVersion?.PublishedAt ?? default;
                    var minorDateColumn = new Markup(FormatPublishedDate(minorDateColumnText));
                    return minorDateColumn;
                }
            }

            public IRenderable RequestedVersion {
                get {
                    var requestedVersionColumn = new Text("not-found".PadRight(length: _requestVersionMaxLength, removeTextInBrackets: true), new Style(foreground: Color.Red3));
                    if (_package.RequestedVersion != null) {
                        var requestedVersionColor = _package.HasVersionMismatch ? Color.Orange1 : Color.Green;
                        requestedVersionColumn = new Text(_package.RequestedVersion.Version.PadRight(length: _versionMaxLength), new Style(foreground: requestedVersionColor));
                    }
                    return requestedVersionColumn;
                }
            }

            public IRenderable RequestedVersionDate {
                get {
                    var requestedVersionDateColumn = new Markup("[Red]not-found[/]");
                    if (_package.RequestedVersion != null) {
                        requestedVersionDateColumn = new Markup(FormatPublishedDate(_package.RequestedVersion.PublishedAt));
                    }
                    return requestedVersionDateColumn;
                }
            }

            public IRenderable PackageUrl => new Text(_package.PackageUrl);

            public IRenderable ProjectUrl => new Text(_package.ProjectUrl);

            public IRenderable VersionUrl {
                get {
                    var versionUrl = _package.LatestVersion.GetUrl(_package.PackageUrl);
                    if (_package.RequestedVersion != null) versionUrl = _package.RequestedVersion.GetUrl(_package.PackageUrl);
                    else if (_settings.Minor) versionUrl = _package.LatestMinorVersion.GetUrl(_package.PackageUrl);
                    return new Text(versionUrl);
                }
            }

            public IRenderable Framework {
                get {
                    var version = _package.DesiredVersion ?? _package.CurrentVersion;
                    var framework = version?.SupportedFrameworks?.Where(p => !p.Version.Preview).FirstOrDefault();
                    var frameworkText = framework?.NickName ?? framework?.ShortName ?? string.Empty;
                    return new Text(frameworkText);
                }
            }

            public IRenderable HasFailed => new Text(_package.HasFailed.ToString());
            public IRenderable IsCurrentVersionInvalid => new Text(_package.IsCurrentVersionInvalid.ToString());
            public IRenderable HasVersionMismatch => new Text(_package.HasVersionMismatch.ToString());
            public IRenderable IsLatestVersion => new Text(_package.IsLatestVersion.ToString());
            public IRenderable IsLatestMinorVersion => new Text(_package.IsLatestMinorVersion.ToString());
            public IRenderable IsRequestedVersionMatch => new Text(_package.IsRequestedVersionMatch.ToString());
        }

        private static string FormatPublishedDate(DateTime date) {
            var datePadding = 15;
            if (date == default || date == DateTime.MinValue) {
                return "[Red]not-found[/]".PadRight(length: datePadding, removeTextInBrackets: true);
            }
            var dateAgo = PackageDateAgo(date);
            var resultText = dateAgo.Key?.Trim()?.Length > 0 ?
                $"{dateAgo.Key.PadLeft("0", 2)} [Gray42]{dateAgo.Value}[/]" :
                $"[Red]{dateAgo.Value}[/]";
            return resultText.PadRight(length: datePadding, removeTextInBrackets: true);
        }

        private static KeyValuePair<string, string> PackageDateAgo(DateTime date) {
            var timeSpan = DateTime.Now.Subtract(date);
            KeyValuePair<string, string> result = timeSpan.TotalSeconds switch {
                <= 60 => new(timeSpan.Seconds.ToString(), "seconds ago"),
                _ => timeSpan.TotalMinutes switch {
                    <= 1 => new("1", "minute ago"),
                    < 60 => new(timeSpan.Minutes.ToString(), "minutes ago"),
                    _ => timeSpan.TotalHours switch {
                        <= 1 => new("1", "hour ago"),
                        < 24 => new(timeSpan.Hours.ToString(), "hours ago"),
                        _ => timeSpan.TotalDays switch {
                            <= 1 => new("1", "day ago"),
                            <= 30 => new(timeSpan.Days.ToString(), "days ago"),

                            <= 60 => new("1", "month ago"),
                            < 365 => new($"{timeSpan.Days / 30}", "months ago"),

                            <= 365 * 2 => new("1", "year ago"),
                            _ => new($"{timeSpan.Days / 365}", "years ago")
                        }
                    }
                }
            };
            return result;
        }
    }
}

using Spectre.Console;
using Spectre.Console.Rendering;
using wng.Model.Dotnet;

namespace wng.Commands.Dotnet {
    internal class DotnetTable(DotnetCommand.Settings settings, List<DotnetFrameworkVersion> allVersions) {

        public virtual void Render() {
            if (allVersions.Count > 0) {

                var versionsTable = new Table().RoundedBorder().BorderColor(Color.Grey);

                var headers = new DotnetTableHeaders(settings);
                headers.Render(versionsTable);

                var rows = new DotnetTableRows(settings, allVersions);
                rows.Render(versionsTable);

                AnsiConsole.Write(versionsTable);
            }
        }

        public class DotnetTableHeaders {
            public List<TableColumn> Headers { get; init; } = [];

            public DotnetTableHeaders(DotnetCommand.Settings settings) {
                var section = settings.Sdk ? "SDK" : "RTE";
                Headers.Add(new TableColumn(section) { Alignment = Justify.Center, NoWrap = true });
                Headers.Add(new TableColumn("Channel") { Alignment = Justify.Left, NoWrap = true });
                Headers.Add(new TableColumn("Name") { Alignment = Justify.Left });
                Headers.Add(new TableColumn("Latest") { Alignment = Justify.Left, NoWrap = true });
                Headers.Add(new TableColumn("Released") { Alignment = Justify.Left, NoWrap = true });
                Headers.Add(new TableColumn("Installed") { Alignment = Justify.Left, NoWrap = true });
                if (settings.Debug) { /* Handle Debug Columns */ }
            }

            public void Render(Table table) => Headers.ForEach(col => table.AddColumn(col));
        }

        public class DotnetTableRows(DotnetCommand.Settings settings, List<DotnetFrameworkVersion> allVersions) {

            public List<DotnetTableRow> Rows { get; init; } = [.. allVersions.Select(v => new DotnetTableRow(settings, v, allVersions))];

            public void Render(Table table) => Rows.ForEach(row => table.AddRow(row.Elements));
        }

        public class DotnetTableRow {

            public IEnumerable<IRenderable> Elements { get; init; }
            private readonly DotnetTableColumns _columns;

            public DotnetTableRow(DotnetCommand.Settings settings, DotnetFrameworkVersion version, List<DotnetFrameworkVersion> allVersions) {
                _columns = new DotnetTableColumns(settings, version, allVersions);

                Elements = [
                    _columns.Status,
                    _columns.Channel,
                    _columns.Name,
                    _columns.ReleaseVersion,
                    _columns.ReleaseDate,
                    _columns.CurrentVersion
                ];
            }

        }

        public class DotnetTableColumns(DotnetCommand.Settings settings, DotnetFrameworkVersion version, List<DotnetFrameworkVersion> allVersions) {

            private readonly DotnetFrameworkVersionRelease release = settings.Sdk ? version.LatestSdk : version.LatestRuntime;
            private readonly int nameMaxLength = allVersions.Max(v => v.Name.Length);
            private readonly int channelMaxLength = allVersions.Max(v => v.ChannelVersion.ToString().Length);
            private readonly int latestVersionMaxLength = allVersions.Max(v => v.LatestSdk?.Version?.ToString()?.Length ?? 0);
            private readonly int currentVersionMaxLength = allVersions.Max(v => v.LatestSdk?.InstalledVersion?.Version?.ToString()?.Length ?? 0);
            private readonly Color notInstalledColor = Color.LightSlateGrey;

            public IRenderable Status {
                get {
                    var statusColumn = release.IsInstalled && release.IsUpToDate ? new Markup("[Lime bold]✓[/]") : new Markup("[yellow bold]●[/]");
                    if (!release.IsInstalled) { statusColumn = new Markup("[LightSlateGrey bold]✗[/]"); }
                    return statusColumn;
                }
            }

            public IRenderable Channel {
                get {
                    var channelColumn = new Text(version.ChannelVersion.ToString().PadRight(length: channelMaxLength, removeTextInBrackets: true));
                    return channelColumn;
                }
            }

            public IRenderable Name {
                get {
                    var nameColor = release.IsInstalled && release.IsUpToDate ? Color.Default : Color.Orange1;
                    if (!release.IsInstalled) nameColor = notInstalledColor;
                    var nameColumn = new Text(version.Name.PadRight(length: nameMaxLength, removeTextInBrackets: true), new Style(foreground: nameColor));
                    return nameColumn;
                }
            }

            public IRenderable ReleaseVersion {
                get {
                    var versionColor = release.IsInstalled ? Color.Aqua : Color.Default;
                    var versionColumn = new Text(release.Version.ToString().PadRight(length: latestVersionMaxLength, removeTextInBrackets: true), new Style(foreground: versionColor));
                    return versionColumn;
                }
            }

            public IRenderable ReleaseDate {
                get {
                    var versionDateColumn = new Markup(FormatPublishedDate(release.ReleaseDate));
                    return versionDateColumn;
                }
            }

            public IRenderable CurrentVersion {
                get {
                    var currentVersionText = release.InstalledVersion?.Version?.ToString() ?? "not-installed";
                    var versionColor = release.IsInstalled && release.IsUpToDate ? Color.Lime : Color.Orange1;
                    if (!release.IsInstalled) versionColor = Color.Gray42;
                    var versionColumn = new Text(currentVersionText.PadRight(length: currentVersionMaxLength, removeTextInBrackets: true), new Style(foreground: versionColor));
                    return versionColumn;
                }
            }
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

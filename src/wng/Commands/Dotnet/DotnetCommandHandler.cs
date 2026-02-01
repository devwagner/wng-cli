using Spectre.Console;
using wng.Model.Dotnet;
using wng.Providers;

namespace wng.Commands.Dotnet {
    internal sealed class DotnetCommandHandler {

        public static void PrintProjectsTable(DotnetCommand.Settings settings, DotNetProjects solution) {
            if (!(solution.Projects?.Count() > 0)) return;
            //TODO: Implement table display
            //var table = new Table().RoundedBorder().BorderColor(Color.Grey);
            //table.AddColumn("PRJ", col => col.Centered());
            //table.AddColumn("Project", col => col.LeftAligned().NoWrap());
        }

        public static void PrintVersionsTable(DotnetCommand.Settings settings, List<DotnetFrameworkVersion> allVersions) {
            var table = new DotnetTable(settings, allVersions);
            table.Render();
        }


        public static async Task<DotNetProjects> GetProjectsAsync(DotnetCommand.Settings settings, DotnetProvider provider, CancellationToken ct = default) {
            DotNetProjects result = default;
            await AnsiConsole.Status().Spinner(Spinner.Known.OrangeBluePulse).SpinnerStyle(Style.Parse("blue")).StartAsync("Analysing NuGet packages...", async ctx => {
                await Extensions.ReasonableWaitAsync();
                try {
                    var solutionPath = settings.Path ?? Directory.GetCurrentDirectory();
                    result = await provider.GetDotNetProjects(solutionPath, ct);
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

using Spectre.Console;
using Spectre.Console.Cli;
using wng.Commands.Dotnet;
using wng.Commands.Npm;
using wng.Commands.NuGet;

return await wng.App.InitializeAsync(args);

namespace wng {
    public sealed class App {

        public static readonly string Version = "1.0.0";

        public static void PrintAppWelcome() {
            var appName = new FigletText("*wng*") { Color = Color.CadetBlue, Justification = Justify.Left };
            var version = new Text($"WNG CLI Version {Version}", new Style(Color.Grey)) { Justification = Justify.Left };

            AnsiConsole.Write(appName);
            AnsiConsole.Write(version);
            AnsiConsole.WriteLine();
        }

        public static async Task<int> InitializeAsync(string[] args) {
            // Create a cancellation token source to handle Ctrl+C
            var cancellationTokenSource = new CancellationTokenSource();

            // Enable hyperlink support in the console
            AnsiConsole.Profile.Capabilities.Links = true;

            // Wire up Console.CancelKeyPress to trigger cancellation
            Console.CancelKeyPress += (_, e) => {
                if(!cancellationTokenSource.IsCancellationRequested) {
                    e.Cancel = true; // Prevent immediate process termination
                    cancellationTokenSource.Cancel();
                    Console.WriteLine("Cancellation requested...");
                }                
            };

            // Configure and run the command app
            var app = new CommandApp();
            app.Configure(config => {

                config.Settings.PropagateExceptions = true;
                config.Settings.StrictParsing = true;
                config.Settings.MaximumIndirectExamples = 10;
                config.Settings.ShowOptionDefaultValues = true;
                config.Settings.CaseSensitivity = CaseSensitivity.None;

#if DEBUG
                config.ValidateExamples();
                config.PropagateExceptions();
#endif
                config.SetApplicationName("wng");
                config.SetApplicationVersion(Version);
                config.SetDefaultStyles();

                NpmCommand.Register(config);
                NuGetCommand.Register(config);
                DotnetCommand.Register(config);
            });
            return await app.RunAsync(args, cancellationTokenSource.Token);
        }
    }
}
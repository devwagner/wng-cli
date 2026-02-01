using System.Text.RegularExpressions;
using wng.Model.Dotnet;

namespace wng {
    public static partial class Extensions {

        [GeneratedRegex(@"^([\w\.]+)\s+([\d\.]+(?:-[\w\.]+)?)\s+\[(.+)\]$")]
        private static partial Regex DotNetInstalledRuntimeRegex();

        public static List<DotnetRuntimeInstalled> ParseDotnetRuntimeOutput(this string output) {
            var runtimes = new List<DotnetRuntimeInstalled>();

            if (string.IsNullOrWhiteSpace(output)) return runtimes;

            var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines) {
                // Format: "Microsoft.NETCore.App 8.0.0 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]"
                //var match = Regex.Match(line, @"^([\w\.]+)\s+([\d\.]+(?:-[\w\.]+)?)\s+\[(.+)\]$");
                var match = DotNetInstalledRuntimeRegex().Match(line);
                if (match.Success) {
                    runtimes.Add(new DotnetRuntimeInstalled {
                        Name = match.Groups[1].Value,
                        Version = match.Groups[2].Value,
                        Path = match.Groups[3].Value
                    });
                }
            }

            return runtimes.OrderBy(r => r.Name).ThenByDescending(r => r.Version).ToList();
        }

        [GeneratedRegex(@"^([\d\.]+(?:-[\w\.]+)?)\s+\[(.+)\]$")]
        private static partial Regex DotNetInstalledSdkRegex();

        public static List<DotnetSdkInstalled> ParseDotnetSdkOutput(this string output) {
            var sdks = new List<DotnetSdkInstalled>();

            if (string.IsNullOrWhiteSpace(output)) return sdks;

            var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines) {
                // Format: "8.0.100 [C:\Program Files\dotnet\sdk]"
                //var match = Regex.Match(line, @"^([\d\.]+(?:-[\w\.]+)?)\s+\[(.+)\]$");
                var match = DotNetInstalledSdkRegex().Match(line);
                if (match.Success) {
                    sdks.Add(new DotnetSdkInstalled {
                        Version = match.Groups[1].Value,
                        Path = match.Groups[2].Value
                    });
                }
            }

            return sdks.OrderByDescending(s => s.Version).ToList();
        }

        [GeneratedRegex(@"<TargetFramework>([^<]+)</TargetFramework>|<TargetFrameworkVersion>([^<]+)</TargetFrameworkVersion>", RegexOptions.IgnoreCase)]
        private static partial Regex DotNetTargetFrameworkRegex();

        public static string GetTargetDotNetFramework(this string csprojContent) {
            if (string.IsNullOrEmpty(csprojContent)) return null;

            var match = DotNetTargetFrameworkRegex().Match(csprojContent);
            if (!match.Success) return null;

            // Group 1 is TargetFramework, Group 2 is TargetFrameworkVersion
            var targetFramework = match.Groups[1].Value;
            var targetFrameworkVersion = match.Groups[2].Value;

            // Return whichever one matched
            if (!string.IsNullOrEmpty(targetFramework)) {
                return targetFramework; // e.g., "net9.0", "net10.0"
            }

            if (!string.IsNullOrEmpty(targetFrameworkVersion)) {
                // Convert old format like "v4.8" to "net48"
                return ConvertFrameworkVersionToMoniker(targetFrameworkVersion);
            }

            return null;
        }

        private static string ConvertFrameworkVersionToMoniker(string frameworkVersion) {
            if (string.IsNullOrEmpty(frameworkVersion)) return frameworkVersion;

            // Remove 'v' prefix and dots: v4.8 -> net48, v4.7.2 -> net472
            var version = frameworkVersion.TrimStart('v', 'V').Replace(".", "");
            return $"net{version}";
        }

        [GeneratedRegex(@"(<Package(?:Reference|Version)\s+Include=""[^""]+""\s+Version="")([^""]+)(""\s*/>)")]
        private static partial Regex NuGetPackageVersionReplaceRegex();

        public static string ReplaceNuGetPackageVersion(this string csprojLine, string packageName, string newVersion) {
            if (string.IsNullOrEmpty(csprojLine)) return csprojLine;
            if (!csprojLine.Contains("Version=", StringComparison.OrdinalIgnoreCase)) return csprojLine;

            // Only replace if the line contains the specific package name
            if (!csprojLine.Contains($"Include=\"{packageName}\"", StringComparison.OrdinalIgnoreCase)) {
                return csprojLine;
            }

            return NuGetPackageVersionReplaceRegex().Replace(csprojLine, match => {
                var prefix = match.Groups[1].Value;
                var suffix = match.Groups[3].Value;
                return $"{prefix}{newVersion}{suffix}";
            });
        }

        public static bool IsNuGetWarningOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Contains(" : warning ", StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatNuGetWarningOutput(this string text, string solutionFile) {
            if (string.IsNullOrEmpty(text)) return text;

            var lines = text.Split(" : ", StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(r => r.Split(". ", StringSplitOptions.RemoveEmptyEntries)).ToList();
            if (lines.Count > 0 && lines[0].Contains('\\')) lines[0] = $"[grey]{lines[0]}[/]"; // Remove first line if it's a file path
            lines = [.. lines.Select(l => l.Contains("warning", StringComparison.OrdinalIgnoreCase) ? $"[yellow]{l}[/]" : l)];
            lines = [.. lines.Select(l => l.Replace(solutionFile, "").Replace("()", "").TrimStart().TrimEnd())];
            lines = [.. lines.Where(l => !string.IsNullOrEmpty(l.Trim()))];

            var resultLine = "[darkOrange]? Warning:[/] " + string.Join(Environment.NewLine, lines);
            resultLine = resultLine.Replace(solutionFile, $"[grey]{solutionFile}[/]");

            return resultLine + Environment.NewLine;
        }

        public static bool IsNuGetRestoredOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Contains("Restored ", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("(in", StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatNuGetRestoreOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return text;
            var resultLine = text.Replace("  Restored ", "[green]✓ Success:[/] [aqua]Restored[/] ")
                                 .Replace("(in", "[grey](in")
                                 .Replace(").", ")[/]");
            return resultLine;
        }

        public static bool IsNuGetDeterminingOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Contains("Determining projects to restore");
        }

        public static string FormatNuGetDeterminingOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return text;
            return $"[Aqua]● Waiting:[/] {text.TrimStart().TrimEnd()}";
        }

        public static bool IsNuGetErrorOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Contains(": error ", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("build failed", StringComparison.OrdinalIgnoreCase) ||
                text.Contains(" error(s)", StringComparison.OrdinalIgnoreCase) ||
                text.Contains(" warning(s)", StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatNuGetErrorOutput(this string text, string solutionFile) {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.Contains("warning(s)", StringComparison.OrdinalIgnoreCase)) {
                return $"{Environment.NewLine}[yellow]{text}[/]";
            }
            if (text.Contains(" error(s)", StringComparison.OrdinalIgnoreCase)) {
                return $"[red bold]{text}[/]";
            }
            if (text.Contains("build failed", StringComparison.OrdinalIgnoreCase)) {
                return $"{Environment.NewLine}[red bold]{text}[/]";
            }

            var lines = text.Split(" : ", StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(r => r.Split(". ", StringSplitOptions.RemoveEmptyEntries)).ToList();
            if (lines.Count > 0 && lines[0].Contains('\\')) lines[0] = $"[grey]{lines[0]}[/]"; // Remove first line if it's a file path
            lines = [.. lines.Select(l => l.Contains("error", StringComparison.OrdinalIgnoreCase) ? $"[LightCoral]{l}[/]" : l)];
            lines = [.. lines.Select(l => l.Replace(solutionFile, "").Replace("()", "").TrimStart().TrimEnd())];
            lines = [.. lines.Where(l => !string.IsNullOrEmpty(l.Trim()))];

            var resultLine = "[red]✗ Error:[/] " + string.Join(Environment.NewLine, lines);
            resultLine = resultLine.Replace(solutionFile, $"[grey]{solutionFile}[/]");

            return Environment.NewLine + resultLine;            
        }

        public static bool IsNuGetErrorAnnotationOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Contains(" -> ", StringComparison.OrdinalIgnoreCase) && text.Contains('\\');
        }

        public static string FormatNuGetErrorAnnotationOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return text;
            return $"[aqua]{text}[/]";
        }

        [GeneratedRegex(@"(?<!<!--\s*)<Package(?:Reference|Version)\s+Include=""(?<name>[^""]+)""\s+Version=""(?<version>[^""]+)""\s*/>(?!\s*-->)", RegexOptions.IgnoreCase)]
        private static partial Regex NuGetPackageReferenceRegex();

        public static Dictionary<string, string> GetNuGetPackageReferences(this string csprojOrPackagePropsContent) {
            if (string.IsNullOrEmpty(csprojOrPackagePropsContent)) return [];

            var packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var matches = NuGetPackageReferenceRegex().Matches(csprojOrPackagePropsContent);

            foreach (Match match in matches) {
                if (match.Success) {
                    var packageName = match.Groups["name"].Value;
                    var version = match.Groups["version"].Value;

                    if (!string.IsNullOrEmpty(packageName) && !string.IsNullOrEmpty(version)) {
                        packages[packageName] = version;
                    }
                }
            }

            return packages;
        }

    }
}

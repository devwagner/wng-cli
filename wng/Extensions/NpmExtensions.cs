using System.Text.RegularExpressions;

namespace wng {
    public static partial class Extensions {

        // Handles: ^5.3.6, ~5.3.6, 5.3.6, >=5.3.6, 5.3.6-alpha.1, 5.3.6-beta, etc.
        // Captures version prefix and number separately to preserve prefix during replacement
        [GeneratedRegex(@"(?<=:\s*"")([~^>=<]*)(\d+\.\d+\.\d+(?:[.-]\w+)*)")]
        private static partial Regex NpmVersionRegex();

        public static string ReplaceVersionInPackageText(this string text, string newVersion) {
            string result = NpmVersionRegex().Replace(text, match => {
                var prefix = match.Groups[1].Value; // Captures ~^>=<
                return prefix + newVersion;
            });
            return result;
        }

        // Matches npm install output patterns like "added 2 packages", "removed 1 package", etc.
        [GeneratedRegex(@"(added|removed|changed|updated|audited|found|installed)\s+(\d+)\s+(packages?|vulnerabilit(?:y|ies))", RegexOptions.IgnoreCase)]
        private static partial Regex NpmInstallOutputRegex();

        public static bool IsNpmInstallOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return false;
            return NpmInstallOutputRegex().IsMatch(text);
        }

        public static string FormatNpmInstallOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return text;

            var result = NpmInstallOutputRegex().Replace(text, match => {
                var action = match.Groups[1].Value.ToLowerInvariant();
                var number = match.Groups[2].Value;
                var targetWord = match.Groups[3].Value;

                // Choose color based on action
                var color = action switch {
                    "added" => "lime",
                    "removed" => "orange1",
                    "changed" => "yellow",
                    "updated" => "deepskyblue1",
                    "audited" => "darkslategray1",
                    "found" => "red",
                    "installed" => "green",
                    _ => "white"
                };

                return $"{match.Groups[1].Value} [{color}]{number}[/] {targetWord}";
            });

            return result;
        }

        public static bool IsNpmWarningOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return false;
            return text.StartsWith("npm WARN", StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatNpmWarningOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return text;
            if (!text.IsNpmWarningOutput()) return text;
            return $"[LightGoldenrod2_2]{text.TrimStart()}[/]";
        }

        public static bool IsNpmErrorOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return false;
            return text.StartsWith("npm error", StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatNpmErrorOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return text;
            if (!text.IsNpmErrorOutput()) return text;
            return $"[LightPink1]{text.TrimStart()}[/]";
        }

        public static bool IsNpmCriticalSeverityOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Contains("critical severity", StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatNpmCriticalSeverityOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return text;
            if (!text.IsNpmCriticalSeverityOutput()) return text;
            return $"[red]{text}[/]";
        }

        // Matches npm funding output like "95 packages are looking for funding"
        [GeneratedRegex(@"(\d+)\s+(packages?)\s+(?:are|is)\s+looking\s+for\s+funding", RegexOptions.IgnoreCase)]
        private static partial Regex NpmFundingOutputRegex();

        public static bool IsNpmFundingOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return false;
            return NpmFundingOutputRegex().IsMatch(text);
        }

        public static string FormatNpmFundingOutput(this string text) {
            if (string.IsNullOrEmpty(text)) return text;

            var result = NpmFundingOutputRegex().Replace(text, match => {
                var number = match.Groups[1].Value;
                var packageWord = match.Groups[2].Value;
                var lookingText = match.Value.Contains("are", StringComparison.OrdinalIgnoreCase) ? "are" : "is";

                return $"[deepskyblue1]{number}[/] {packageWord} {lookingText} looking for funding";
            });

            return result;
        }

        // Matches command annotations like `npm fund`, `npm audit`, etc.
        [GeneratedRegex(@"`([^`]+)`", RegexOptions.None)]
        private static partial Regex CommandAnnotationRegex();

        public static bool HasCommandAnnotation(this string text) {
            if (string.IsNullOrEmpty(text)) return false;
            return CommandAnnotationRegex().IsMatch(text);
        }

        public static string FormatCommandAnnotation(this string text) {
            if (string.IsNullOrEmpty(text)) return text;

            var result = CommandAnnotationRegex().Replace(text, match => {
                var command = match.Groups[1].Value;
                return $"[blue]`{command}`[/]";
            });

            return result;
        }
    }
}

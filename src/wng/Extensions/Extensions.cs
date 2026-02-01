using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using System.Text;
using System.Text.RegularExpressions;

namespace wng {
    public static partial class Extensions {

        public static string GetFileContents(string filePath) {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) {
                throw new FileNotFoundException("Could not find file.", fileInfo.FullName);
            }
            return File.ReadAllText(fileInfo.FullName);
        }

        public static bool HasCustomVersionSuffix(this string version) {
            if (string.IsNullOrWhiteSpace(version)) { return false; }
            if (!version.Contains('-')) { return false; }
            //Any combination of dashes is considered a custom version
            //ex. 1.0.0-beta, 2.1.0-alpha.1, 3.0.0-rc2, 2.2.0-nightly, 14.7.2-preview, etc.
            return true;
        }

        public static IEnumerable<R> SelectWithIndex<T, R>(this IEnumerable<T> source, Func<T, int, R> action) {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(action);
            IEnumerable<R> result = [];

            int index = 0;
            foreach (var item in source) {
                var resultItem = action(item, index);
                if (resultItem != null) {
                    result = [.. result, resultItem];
                    index++;
                }
            }
            return result;
        }

        public static int FindFirstIndex<T>(this IEnumerable<T> source, Func<T, bool> predicate) {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);
            int index = 0;
            foreach (var item in source) {
                if (predicate(item)) {
                    return index;
                }
                index++;
            }
            return -1;
        }

        public static string ReplaceAll(this string source, IEnumerable<string> items, string replacement) {
            var result = source;
            foreach (var item in items) {
                result = result.Replace(item, replacement);
            }
            return result;
        }

        public static T ItemOrDefault<T>(this IEnumerable<T> list, int index) {
            if (index < 0 || index >= list.Count()) {
                return default;
            }
            return list.ElementAt(index);
        }

        public static int ToIntOrDefault(this string str) {
            if (int.TryParse(str.RemoveNonNumericCharacters(), out int result)) {
                return result;
            }
            return 0;
        }

        public static string RemoveNonLetterCharacters(this string str) {
            var sb = new StringBuilder();
            foreach (var c in str) {
                if (char.IsLetter(c)) {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static string RemoveNonNumericCharacters(this string str) {
            var sb = new StringBuilder();
            foreach (var c in str) {
                if (char.IsDigit(c)) {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static string PadLeft(this string text, string prefix = " ", int length = 1, bool removeTextInBrackets = false) {
            if (removeTextInBrackets) text = text.RemoveTextInBrackets();
            var size = length - (text?.Length ?? 0);
            if (!(size > 0)) return text;
            for (int i = 0; i < size; i++) text = prefix + text;
            return text;
        }

        public static string PadRight(this string text, string prefix = " ", int length = 1, bool removeTextInBrackets = false) {
            text ??= string.Empty;
            var textLength = removeTextInBrackets ? text.RemoveTextInBrackets().Length : text.Length;
            var size = length - textLength;
            if (!(size > 0)) return text;
            for (int i = 0; i < size; i++) text += prefix;
            return text;
        }

        public static string Truncate(this string text, int maxLength, string truncationIndicator = "...") {
            if (string.IsNullOrEmpty(text) || maxLength <= 0) return string.Empty;
            if (text.Length <= maxLength) return text;
            if (truncationIndicator.Length >= maxLength) return truncationIndicator[..maxLength];
            return string.Concat(text.AsSpan(0, maxLength - truncationIndicator.Length), truncationIndicator);
        }

        [GeneratedRegex(@"\[.*?\]")] private static partial Regex SquareBracketsRegex();
        [GeneratedRegex(@"\{.*?\}")] private static partial Regex CurlyBracketsRegex();
        [GeneratedRegex(@"\(.*?\)")] private static partial Regex ParenthesesRegex();
        [GeneratedRegex(@"<.*?>")] private static partial Regex AngleBracketsRegex();

        public static string RemoveTextInBrackets(this string str, bool includeSquareBrackets = true, bool includeCurlyBraces = true, bool includeParentheses = true, bool includeAngleBrackets = false) {
            if (string.IsNullOrEmpty(str)) return str;

            var result = str;
            if (includeSquareBrackets) result = SquareBracketsRegex().Replace(result, string.Empty);
            if (includeCurlyBraces) result = CurlyBracketsRegex().Replace(result, string.Empty);
            if (includeParentheses) result = ParenthesesRegex().Replace(result, string.Empty);
            if (includeAngleBrackets) result = AngleBracketsRegex().Replace(result, string.Empty);

            return result;
        }
        
        public static void SetDefaultStyles(this IConfigurator config) {
            config.Settings.HelpProviderStyles = new HelpProviderStyle {
                Description = new DescriptionStyle {
                    Header = "yellow",
                },
                Usage = new UsageStyle {
                    Header = "yellow",
                    Options = "dim",
                    CurrentCommand = "yellow",
                },
                Examples = new ExampleStyle {
                    Header = "yellow",
                    Arguments = "dim"
                },
                Options = new OptionStyle {
                    Header = "yellow",
                },
                Commands = new CommandStyle {
                    Header = "yellow",
                    ChildCommand = "yellow",
                    RequiredArgument = "darkorange",
                },
                Arguments = new ArgumentStyle {
                    Header = "yellow",
                    RequiredArgument = "blue",
                    OptionalArgument = "lime",
                },
            };
        }

        public static readonly int LocalDelayMs = 500;
        public static void ReasonableWait(int? delayMs = default) => Thread.Sleep(delayMs ?? LocalDelayMs);
        public static async Task ReasonableWaitAsync(int? delayMs = default, CancellationToken ct = default) => await Task.Delay(delayMs ?? LocalDelayMs, ct);
    }
}

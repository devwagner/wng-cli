using System.Text.Json;
using System.Text.Json.Serialization;

namespace wng {
    public static class SerializationExtensions {

        public static readonly JsonSerializerOptions JsonSerializerOptions = new() {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            IgnoreReadOnlyFields = true,
            IgnoreReadOnlyProperties = false,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault
        };

        public static void SetupDefaultSerializationOptions(this JsonSerializerOptions options) {
            options.AllowTrailingCommas = JsonSerializerOptions.AllowTrailingCommas;
            options.PropertyNameCaseInsensitive = JsonSerializerOptions.PropertyNameCaseInsensitive;
            options.IgnoreReadOnlyFields = JsonSerializerOptions.IgnoreReadOnlyFields;
            options.IgnoreReadOnlyProperties = JsonSerializerOptions.IgnoreReadOnlyProperties;
            options.WriteIndented = JsonSerializerOptions.WriteIndented;
            options.ReadCommentHandling = JsonSerializerOptions.ReadCommentHandling;
            options.PropertyNamingPolicy = JsonSerializerOptions.PropertyNamingPolicy;
            options.UnmappedMemberHandling = JsonSerializerOptions.UnmappedMemberHandling;
            options.NumberHandling = JsonSerializerOptions.NumberHandling;
            options.DefaultIgnoreCondition = JsonSerializerOptions.DefaultIgnoreCondition;
            foreach (var converter in JsonSerializerOptions.Converters) { options.Converters.Add(converter); }
        }
    }

    public class NullBoolJsonConverter : JsonConverter<bool> {
        public override bool HandleNull => true;

        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            try {
                var stringRepresentation = reader.GetString()?.ToLower() ?? string.Empty;
                if (new List<string> { "1", "true", "y", "yes" }.Contains(stringRepresentation)) return true;
            }
            catch (Exception ex) { ex.ToString(); /* Just ignore at this point */ }
            try {
                return reader.GetBoolean();
            }
            catch (Exception ex) { ex.ToString(); /* If the value is null in the request Json, the reader will throw an invalid operation exception, and the browser will receive an error 400 (Bad Request) */ }
            return false;
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) { writer.WriteBooleanValue(value); }
    }

    public class NullIntJsonConverter : JsonConverter<int> {
        public override bool HandleNull => true;

        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            try {
                if (reader.TryGetInt32(out int value)) return value;
            }
            catch (Exception ex) { ex.ToString(); /* If the value is null in the request Json, the reader will throw an invalid operation exception, and the browser will receive an error 400 (Bad Request) */ }
            return 0;
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) { writer.WriteNumberValue(value); }
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace wng.Model.Npm {
    public class NpmAuditResult {
        [JsonPropertyName("auditReportVersion")]
        public int AuditReportVersion { get; set; }

        [JsonPropertyName("vulnerabilities")]
        public Dictionary<string, NpmVulnerability> Vulnerabilities { get; set; } = [];
    }

    public class NpmVulnerability {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("severity")]
        public string Severity { get; set; } = string.Empty;

        [JsonPropertyName("isDirect")]
        public bool IsDirect { get; set; }

        [JsonPropertyName("via")]
        [JsonConverter(typeof(NpmVulnerabilityViaConverter))]
        public List<object> Via { get; set; } = [];

        public List<NpmVulnerabilityDetail> ViaDetails => [.. Via.OfType<NpmVulnerabilityDetail>()];

        [JsonPropertyName("effects")]
        public List<string> Effects { get; set; } = [];

        [JsonPropertyName("range")]
        public string Range { get; set; } = string.Empty;

        [JsonPropertyName("nodes")]
        public List<string> Nodes { get; set; } = [];

        [JsonPropertyName("fixAvailable")]
        public object FixAvailable { get; set; } = false;
    }

    public class NpmVulnerabilityDetail {
        [JsonPropertyName("source")]
        public int Source { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("dependency")]
        public string Dependency { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("severity")]
        public string Severity { get; set; } = string.Empty;

        [JsonPropertyName("range")]
        public string Range { get; set; } = string.Empty;
    }

    public class NpmVulnerabilityViaConverter : JsonConverter<List<object>> {
        public override List<object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            var result = new List<object>();

            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException();

            while (reader.Read()) {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (reader.TokenType == JsonTokenType.String) {
                    // Handle string values (package names)
                    result.Add(reader.GetString());
                }
                else if (reader.TokenType == JsonTokenType.StartObject) {
                    // Handle object values (vulnerability details)
                    var detail = JsonSerializer.Deserialize<NpmVulnerabilityDetail>(ref reader, options);
                    result.Add(detail);
                }
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, List<object> value, JsonSerializerOptions options) {
            writer.WriteStartArray();
            foreach (var item in value) {
                if (item is string str)
                    writer.WriteStringValue(str);
                else
                    JsonSerializer.Serialize(writer, item, options);
            }
            writer.WriteEndArray();
        }
    }
}
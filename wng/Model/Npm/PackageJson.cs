using System.Text.Json.Serialization;

namespace wng.Model.Npm {
    public class PackageJson {

        [JsonPropertyName("dependencies")]
        public Dictionary<string, string> Dependencies { get; set; } = [];

        [JsonPropertyName("devDependencies")]
        public Dictionary<string, string> DevDependencies { get; set; } = [];

    }
}
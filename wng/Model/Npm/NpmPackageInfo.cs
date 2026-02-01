using System.Text.Json.Serialization;

namespace wng.Model.Npm {
    internal class NpmPackageInfo {

        [JsonPropertyName("_id")]
        public string Id { get; set; }

        [JsonPropertyName("_rev")]
        public string Revision { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        //TODO: Not being used currently, commented to preserve performance during deserialization
        //[JsonPropertyName("versions")]
        //public Dictionary<string, NpmVersionInfo> Versions { get; set; }

        //TODO: Not being used currently, commented to preserve performance during deserialization
        //[JsonPropertyName("dist-tags")]
        //public Dictionary<string, string> DistTags { get; set; }

        [JsonPropertyName("time")]
        public Dictionary<string, DateTime> Time { get; set; }

        [JsonPropertyName("homepage")]
        public string HomePage { get; set; }

        [JsonPropertyName("license")]
        public string License { get; set; }
    }

    internal class NpmVersionInfo {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("homepage")]
        public string Homepage { get; set; }

        [JsonPropertyName("repository")]
        public NpmRepository Repository { get; set; }

        [JsonPropertyName("bugs")]
        public NpmBugs Bugs { get; set; }

        [JsonPropertyName("license")]
        public string License { get; set; }

        [JsonPropertyName("dependencies")]
        public Dictionary<string, string> Dependencies { get; set; }

        [JsonPropertyName("peerDependencies")]
        public Dictionary<string, string> PeerDependencies { get; set; }

        [JsonPropertyName("dist")]
        public NpmDist Dist { get; set; }
    }

    internal class NpmRepository {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    internal class NpmBugs {
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    internal class NpmDist {
        [JsonPropertyName("tarball")]
        public string Tarball { get; set; }

        [JsonPropertyName("shasum")]
        public string Shasum { get; set; }

        [JsonPropertyName("integrity")]
        public string Integrity { get; set; }
    }
}
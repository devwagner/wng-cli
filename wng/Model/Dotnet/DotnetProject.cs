using NuGet.Protocol;
using System.Text.Json.Serialization;

namespace wng.Model.Dotnet {

    public readonly struct DotNetProjects(string name, IEnumerable<DotNetProject> projects = null) {
        public string Name { get; init; } = name;
        public IEnumerable<DotNetProject> Projects { get; init; } = projects ?? [];
    }

    public struct DotNetProject {

        public DotNetProject() {
            Name = string.Empty;
            FilePath = string.Empty;
            Packages = [];
            IsValid = false;
            FrameworkVersion = null;
        }

        public DotNetProject(FileInfo projectFilePath, string frameworkVersion = null) {
            Name = projectFilePath.Name;
            FilePath = projectFilePath.FullName;
            Packages = [];
            FrameworkVersion = frameworkVersion;
            IsValid = projectFilePath.Exists && !string.IsNullOrEmpty(Name);
        }

        public DotNetProject(string name, string filePath, IEnumerable<NuGetPackage> packages, string frameworkVersion = null) {
            Name = name;
            FilePath = filePath;
            Packages = [.. packages];
            FrameworkVersion = frameworkVersion;
            IsValid = !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(filePath) && (packages?.Any() ?? false);
        }

        public string Name { get; init; }

        public string FilePath { get; init; }

        public bool IsValid { get; init; } = false;

        public string FrameworkVersion { get; init; }

        public IEnumerable<NuGetPackage> Packages { get; private set; } = [];

        public readonly void UpdatePackageProjectName() {
            foreach (var package in Packages) {
                package.SetProjectName(Name);
            }
        }

        public DotNetProject Filter(IEnumerable<string> inclusivePackages, IEnumerable<string> ignorePackages) {
            if (inclusivePackages?.Any() == true) {
                Packages = [.. Packages.Where(p => inclusivePackages.Any(name => p.Name.Contains(name, StringComparison.InvariantCultureIgnoreCase)))];
            }
            if (ignorePackages?.Any() == true) {
                Packages = [.. Packages.Where(p => !ignorePackages.Any(ip => p.Name.Contains(ip, StringComparison.InvariantCultureIgnoreCase)))];
            }
            return this;
        }
    }

    public class DotNetCommandRequest(string projectFilePath) {
        public string Command { get; set; }
        public string ProjectFilePath { get; set; } = projectFilePath;
        public Action<bool, string> LocationAction { get; set; }
        public Action<string> OnDataOutput { get; set; }
        public Action<string> OnErrorOutput { get; set; }
    }

    public enum DotnetFrameworkType { Unknown = 0, NetFramework = 1, NetCore = 2, }

    public class DotnetFrameworkVersions {
        public bool Failed { get; set; }
        public string Message { get; set; }
        public DotnetInstalledVersions InstalledVersions { get; set; }
        
        public DotnetFrameworkVersionList NetCore { get; set; } = new(DotnetFrameworkType.NetCore);
        
        //We are not supporting .NET Framework at this time, but we leave the structure in place for future use
        public DotnetFrameworkVersionList NetFramework { get; set; } = new(DotnetFrameworkType.NetFramework);

        public void SetFailure(string message) {
            Failed = true;
            Message = message;
        }
    }

    public class DotnetFrameworkVersionList(DotnetFrameworkType type = DotnetFrameworkType.Unknown) {
        public DotnetFrameworkType Type { get; set;  } = type;
        public List<DotnetFrameworkVersion> Versions { get; set; } = [];
    }

    public class DotnetFrameworkVersion {

        public DotnetFrameworkVersion(DotnetReleaseInfo release, DotnetInstalledVersions installedVersions) {
            var shortName = $"net{release.ChannelVersion}";
            Name = $"{release.Product} {release.ChannelVersion}";
            ShortName = shortName;
            SupportPhase = release.SupportPhase;
            ReleaseType = release.ReleaseType;

            ChannelVersion = new PackageVersion(release.ChannelVersion);
            ReleaseDate = DateTime.Parse(release.LatestReleaseDate);
            EndOfLifeDate = DateTime.Parse(release.EolDate);

            LatestSdk = DotnetFrameworkVersionRelease.Sdk(release, installedVersions, ChannelVersion);
            LatestRuntime = DotnetFrameworkVersionRelease.Runtime(release, installedVersions, ChannelVersion);
        }

        public string Name { get; set; }
        public string ShortName { get; set; }
        public string SupportPhase { get; set; }
        public string ReleaseType { get; set; }
        public PackageVersion ChannelVersion { get; set; }
        public DateTime ReleaseDate { get; set; }
        public DateTime EndOfLifeDate { get; set; }

        public DotnetFrameworkVersionRelease LatestSdk { get; set; }
        public DotnetFrameworkVersionRelease LatestRuntime { get; set; }
    }

    public class DotnetFrameworkVersionRelease {
        
        public PackageVersion Version { get; set; }
        public DotnetRelease Release { get; set; }
        public DateTime ReleaseDate { get; set; }
        public IDotnetInstalled InstalledVersion { get; set; }
        public bool IsInstalled => InstalledVersion != null;
        public bool IsUpToDate => InstalledVersion?.Version == Version;

        public static DotnetFrameworkVersionRelease Sdk(DotnetReleaseInfo releaseInfo, DotnetInstalledVersions installedVersions, PackageVersion channelVersion) {
            var release = releaseInfo.Metadata.Releases
                .Where(r => r.Sdk.Version.Equals(releaseInfo.LatestSdk, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            var result = new DotnetFrameworkVersionRelease {
                Release = release,
                ReleaseDate = DateTime.Parse(release.ReleaseDate),
                Version = new PackageVersion(release.Sdk.Version),
                InstalledVersion = installedVersions?.Sdks?
                    .OrderByDescending(v => v.Version)
                    .FirstOrDefault(v => v.ChannelVersion == channelVersion)
            };
            return result;
        }

        public static DotnetFrameworkVersionRelease Runtime(DotnetReleaseInfo releaseInfo, DotnetInstalledVersions installedVersions, PackageVersion channelVersion) {
            var release = releaseInfo.Metadata.Releases
                .Where(r => r.Runtime.Version.Equals(releaseInfo.LatestRuntime, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            var result = new DotnetFrameworkVersionRelease {
                Release = release,
                ReleaseDate = DateTime.Parse(release.ReleaseDate),
                Version = new PackageVersion(release.Runtime.Version),
                InstalledVersion = installedVersions?.Runtimes?
                    .OrderByDescending(v => v.Version)
                    .FirstOrDefault(v => v.ChannelVersion == channelVersion)
            };
            return result;
        }

        public override string ToString() {
            return $"{Version} -> {InstalledVersion?.Version ?? "N/A" }";
        }
    }

    public class DotnetReleasesIndex {
        [JsonPropertyName("releases-index")]
        public List<DotnetReleaseInfo> ReleasesIndex { get; set; } = [];
    }

    public class DotnetReleaseInfo {
        [JsonPropertyName("channel-version")]
        public string ChannelVersion { get; set; }

        [JsonPropertyName("latest-release")]
        public string LatestRelease { get; set; }

        [JsonPropertyName("latest-release-date")]
        public string LatestReleaseDate { get; set; }

        [JsonPropertyName("latest-sdk")]
        public string LatestSdk { get; set; }

        [JsonPropertyName("latest-runtime")]
        public string LatestRuntime { get; set; }

        [JsonPropertyName("product")]
        public string Product { get; set; }

        [JsonPropertyName("support-phase")]
        public string SupportPhase { get; set; }

        [JsonPropertyName("eol-date")]
        public string EolDate { get; set; }

        [JsonPropertyName("releases.json")]
        public string ReleasesJsonUrl { get; set; }

        [JsonPropertyName("security")]
        public bool Security { get; set; }

        [JsonPropertyName("release-type")]
        public string ReleaseType { get; set; }

        [JsonIgnore]
        public DotnetReleaseMetadata Metadata { get; set; }
    }

    public class DotnetReleaseMetadata {
        [JsonPropertyName("channel-version")]
        public string ChannelVersion { get; set; }

        [JsonPropertyName("latest-release")]
        public string LatestRelease { get; set; }

        [JsonPropertyName("latest-release-date")]
        public string LatestReleaseDate { get; set; }

        [JsonPropertyName("latest-runtime")]
        public string LatestRuntime { get; set; }

        [JsonPropertyName("latest-sdk")]
        public string LatestSdk { get; set; }

        [JsonPropertyName("product")]
        public string Product { get; set; }

        [JsonPropertyName("support-phase")]
        public string SupportPhase { get; set; }

        [JsonPropertyName("eol-date")]
        public string EolDate { get; set; }

        [JsonPropertyName("security")]
        public bool Security { get; set; }

        [JsonPropertyName("releases")]
        public List<DotnetRelease> Releases { get; set; } = [];
    }

    public class DotnetRelease {
        [JsonPropertyName("release-date")]
        public string ReleaseDate { get; set; }

        [JsonPropertyName("release-version")]
        public string ReleaseVersion { get; set; }

        [JsonPropertyName("security")]
        public bool Security { get; set; }

        [JsonPropertyName("cve-list")]
        public List<DotnetCve> CveList { get; set; } = [];

        [JsonPropertyName("release-notes")]
        public string ReleaseNotes { get; set; }

        [JsonPropertyName("runtime")]
        public DotnetRuntimeInfo Runtime { get; set; }

        [JsonPropertyName("sdk")]
        public DotnetSdkInfo Sdk { get; set; }

        [JsonPropertyName("sdks")]
        public List<DotnetSdkInfo> Sdks { get; set; } = [];

        [JsonPropertyName("aspnetcore-runtime")]
        public DotnetRuntimeInfo AspNetCoreRuntime { get; set; }

        [JsonPropertyName("windowsdesktop")]
        public DotnetRuntimeInfo WindowsDesktop { get; set; }
    }

    public class DotnetCve {
        [JsonPropertyName("cve-id")]
        public string CveId { get; set; }

        [JsonPropertyName("cve-url")]
        public string CveUrl { get; set; }
    }

    public class DotnetRuntimeInfo {
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("version-display")]
        public string VersionDisplay { get; set; }

        [JsonPropertyName("files")]
        public List<DotnetFile> Files { get; set; } = [];
    }

    public class DotnetSdkInfo {
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("version-display")]
        public string VersionDisplay { get; set; }

        [JsonPropertyName("runtime-version")]
        public string RuntimeVersion { get; set; }

        [JsonPropertyName("vs-version")]
        public string VsVersion { get; set; }

        [JsonPropertyName("vs-mac-version")]
        public string VsMacVersion { get; set; }

        [JsonPropertyName("files")]
        public List<DotnetFile> Files { get; set; } = [];
    }

    public class DotnetFile {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("rid")]
        public string Rid { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("hash")]
        public string Hash { get; set; }
    }

    public class DotnetInstalledVersions {
        public bool IsInstalled { get; set; }
        public bool Failed { get; set; }
        public string Message { get; set; }
        public List<DotnetSdkInstalled> Sdks { get; set; } = [];
        public List<DotnetRuntimeInstalled> Runtimes { get; set; } = [];

        public void SetFailure(string message) {
            Failed = true;
            Message = message;
        }

        public PackageVersion GetLatestSdkVersion() => Sdks
            .OrderByDescending(r => r.Version)
            .FirstOrDefault()?.Version;

        public List<PackageVersion> GetNetCoreRuntimeVersions() => [.. Runtimes
            .Where(r => r.Name == "Microsoft.NETCore.App")
            .OrderByDescending(r => r.Version)
            .Select(r => r.Version)];
    }

    public interface IDotnetInstalled {
        PackageVersion Version { get; }
        string Path { get; }
        PackageVersion ChannelVersion { get; }
    }

    public class DotnetSdkInstalled : IDotnetInstalled {
        public PackageVersion Version { get; set; }
        public string Path { get; set; }
        public string DotnetVersionCode => $"net{ChannelVersion}";
        public PackageVersion ChannelVersion {
            get {
                if (Version.Major?.Length > 0 && Version.Minor?.Length > 0) {
                    return new PackageVersion($"{Version.Major}.{Version.Minor}");
                }
                return Version;
            }
        }

        public override string ToString() {
            return $"{DotnetVersionCode} - {Version}";
        }
    }

    public class DotnetRuntimeInstalled : IDotnetInstalled {
        public string Name { get; set; }
        public PackageVersion Version { get; set; }
        public string Path { get; set; }
        public PackageVersion ChannelVersion {
            get {
                if(Version.Major?.Length > 0 &&Version.Minor?.Length > 0) {
                    return new PackageVersion($"{Version.Major}.{Version.Minor}");
                }
                return Version;
            }
        }

        public override string ToString() {
            return $"{Name} {Version}";
        }
    }
}

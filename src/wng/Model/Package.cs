
using System.Reflection.Metadata.Ecma335;

namespace wng.Model {

    public enum PackageSource { Unknown = 0, NuGet, Npm }

    public class Package {

        public Package(string name, string version, PackageSource source) {
            this.Source = source;
            this.Name = name;
            this.CurrentVersion = version;
        }

        public Package(KeyValuePair<string, string> dependency, PackageSource source, int order = 0) {
            this.Name = dependency.Key;
            this.Order = order;
            this.Source = source;
            this.CurrentVersion = dependency.Value;
        }

        public PackageSource Source { get; init; }

        public string ProjectName { get; private set; }

        public string Name { get; set; }

        public int Order { get; set; }

        public bool DevDependency { get; set; }

        public int? RequestedMajorVersion { get; private set; }

        public bool IsRequestedVersionInvalid { get; private set; }

        public bool KeepMajorVersion { get; private set; }

        public PackageVersion RequestedVersion { get; private set; }

        public PackageVersion CurrentVersion { get; set; }

        public PackageVersion LatestVersion { get; private set; }

        public PackageVersion LatestMinorVersion { get; private set; }

        public List<PackageVersion> AllVersions { get; init; } = [];

        public string VersionUrl { get; set; }

        public string PackageUrl { get; set; }

        public string ProjectUrl { get; set; }

        public bool IsCurrentVersionInvalid { get; private set; }

        public bool HasFailed { get; private set; }

        public string FailureMessage { get; private set; }

        public bool IsLatestVersion => CurrentVersion == LatestVersion;

        public bool IsLatestMinorVersion => CurrentVersion == LatestMinorVersion;

        public bool IsRequestedVersionMatch => RequestedVersion != null && RequestedVersion == CurrentVersion;

        public bool IsRequestedVersionLatest => RequestedVersion != null && RequestedVersion == LatestVersion;

        public bool HasVersionMismatch =>
            //We have no request version
            (RequestedVersion == null &&
                //We should neet keep the major, and the last version does not match
                ((!KeepMajorVersion && !IsLatestVersion) ||
                //We should keep the major, and the last minor version does not match
                (KeepMajorVersion && !IsLatestMinorVersion))) ||
            //We have a requested version, and it does not match
            (RequestedVersion != null && CurrentVersion != RequestedVersion);

        public PackageVersion DesiredVersion => RequestedVersion ?? (KeepMajorVersion ? LatestMinorVersion : LatestVersion);

        public void SetFailure(string message) {
            HasFailed = true;
            FailureMessage = message;
        }

        public void SetFailure(Exception ex) {
            HasFailed = true;
            FailureMessage = ex.Message;
        }

        public void ClearFailure() {
            HasFailed = false;
            FailureMessage = null;
        }

        public void SetCommandSettings(bool keepMajorVersion, int? requestedMajorVersion) {
            this.KeepMajorVersion = keepMajorVersion;
            this.RequestedMajorVersion = requestedMajorVersion;
        }

        public void SetVersions(List<PackageVersion> allVersions) {

            FilterVersions(allVersions);
            LatestMinorVersion = null;
            LatestVersion = null;

            AllVersions.Clear();
            AllVersions.AddRange(allVersions);
            if (AllVersions.Count == 0) return;

            LatestVersion = AllVersions
                .OrderByDescending(v => v)
                .FirstOrDefault() ?? AllVersions.OrderByDescending(v => v).First();

            LatestMinorVersion = AllVersions
                .Where(v => v.Major == CurrentVersion.Major)
                .OrderByDescending(v => v)
                .FirstOrDefault() ?? CurrentVersion;

            var currentVersion = CurrentVersion.Major == "*" ?
                allVersions.OrderByDescending(v => v).FirstOrDefault() :
                allVersions.FirstOrDefault(v => v.ParsedVersion == CurrentVersion.ParsedVersion);

            if (currentVersion != null) {
                CurrentVersion = currentVersion;
                IsCurrentVersionInvalid = false;
            }
            else { IsCurrentVersionInvalid = true; }

            if (RequestedMajorVersion > 0) {
                var requestedVersion = allVersions
                    .Where(v => v.Major == RequestedMajorVersion.ToString())
                    .OrderByDescending(v => v)
                    .FirstOrDefault();

                if (requestedVersion != null) {
                    RequestedVersion = requestedVersion;
                    IsRequestedVersionInvalid = false;
                }
                else { IsRequestedVersionInvalid = true; }
            }

            ProcessEdgeVersions();
        }

        public void FilterVersions(List<PackageVersion> versions) {
            // Syncfusion Angular Compabitility packages have ngcc versions
            // that should be ignored if the current version is not using it
            if (!CurrentVersion.Version.Contains("ngcc", StringComparison.InvariantCultureIgnoreCase))
                versions = [.. versions.Where(v => !v.Version.Contains("ngcc", StringComparison.InvariantCultureIgnoreCase))];
        }

        public void ProcessEdgeVersions() {
            // Syncfusion Angular Compabitility packages have ngcc versions
            // that should be considered only if the current version is using it
            if (CurrentVersion.Version.Contains("ngcc", StringComparison.InvariantCultureIgnoreCase)) {

                LatestVersion = AllVersions
                    .Where(v => v.Version.Contains("ngcc", StringComparison.InvariantCultureIgnoreCase))
                    .OrderByDescending(v => v)
                    .FirstOrDefault() ?? AllVersions.OrderByDescending(v => v).First();

                LatestMinorVersion = AllVersions
                    .Where(v => v.Major == CurrentVersion.Major && v.Version.Contains("ngcc", StringComparison.InvariantCultureIgnoreCase))
                    .OrderByDescending(v => v)
                    .FirstOrDefault() ?? CurrentVersion;

                if (RequestedMajorVersion > 0) {
                    var requestedVersion = AllVersions
                        .Where(v => v.Major == RequestedMajorVersion.ToString() && v.Version.Contains("ngcc", StringComparison.InvariantCultureIgnoreCase))
                        .OrderByDescending(v => v)
                        .FirstOrDefault();

                    if (requestedVersion != null) {
                        RequestedVersion = requestedVersion;
                        IsRequestedVersionInvalid = false;
                    }
                    else { IsRequestedVersionInvalid = true; }
                }
            }
        }

        public void SetProjectName(string projectName) {
            ProjectName = projectName;
        }

        public override string ToString() {
            return $"{Name}: {CurrentVersion} - {AllVersions.Count} versions";
        }
    }

    public enum PackageVersionType {
        Release,
        Alpha,
        Beta,
        Next,
        Dev,
        Nightly,
        Custom,
        ReleaseCandidate
    }

    public class PackageVersion : IComparable<PackageVersion> {

        public PackageVersion(string version, DateTime publishedAt = default) {
            InitializeVersion(version);
            PublishedAt = publishedAt;
        }

        private void InitializeVersion(string version) {
            version ??= "0.0.0";
            Version = version;
            ParsedVersion = ParseVersion(version);
            Type = ParseType(version);

            var versionParts = this.ParsedVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
            Major = versionParts.ItemOrDefault(0);
            Minor = versionParts.ItemOrDefault(1);
            Build = versionParts.ItemOrDefault(2);
            Revision1 = versionParts.ItemOrDefault(3);
            Revision2 = versionParts.ItemOrDefault(4);
            Revision3 = versionParts.ItemOrDefault(5);
            Revision4 = versionParts.ItemOrDefault(6);
        }

        public string Version { get; private set; }

        public string ParsedVersion { get; private set; }

        public string Major { get; private set; }

        public string Minor { get; private set; }

        public string Build { get; private set; }

        public string Revision1 { get; private set; }

        public string Revision2 { get; private set; }

        public string Revision3 { get; private set; }

        public string Revision4 { get; private set; }

        public PackageVersionType Type { get; private set; }

        public bool IsPreRelease => Type != PackageVersionType.Release;

        public DateTime PublishedAt { get; private set; }

        public List<PackageVulnerability> Vulnerabilities { get; set; } = [];

        public List<PackageFramework> SupportedFrameworks { get; private set; } = [];

        public bool Preview => Version?.Contains('-') == true;

        public void SetSupportedFrameworks(IEnumerable<PackageFramework> frameworks) {
            SupportedFrameworks = [.. frameworks];
        }

        public void SetPublishedAt(DateTime publishedAt) {
            PublishedAt = publishedAt;
        }

        public string GetUrl(string packageUrl) {
            if (string.IsNullOrEmpty(packageUrl)) return null;
            return $"{packageUrl}/v/{Version}";
        }

        private static PackageVersionType ParseType(string version) {
            if (version.Contains("-alpha", StringComparison.InvariantCultureIgnoreCase)) return PackageVersionType.Alpha;
            else if (version.Contains("-beta", StringComparison.InvariantCultureIgnoreCase)) return PackageVersionType.Beta;
            else if (version.Contains("-rc", StringComparison.InvariantCultureIgnoreCase)) return PackageVersionType.ReleaseCandidate;
            else if (version.Contains("-next", StringComparison.InvariantCultureIgnoreCase)) return PackageVersionType.Next;
            else if (version.Contains("-dev", StringComparison.InvariantCultureIgnoreCase)) return PackageVersionType.Dev;
            else if (version.Contains("-nightly", StringComparison.InvariantCultureIgnoreCase)) return PackageVersionType.Nightly;
            else if (version.HasCustomVersionSuffix()) return PackageVersionType.Custom;
            return PackageVersionType.Release;
        }

        private static string ParseVersion(string version) {
            List<string> operators = [" - ", " || ", " && "];
            version = version.ReplaceAll(operators, " ");
            version = version.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();

            List<string> symbols = ["^", "~", ">", "<", "="];
            version = version.ReplaceAll(symbols, string.Empty);

            return version;
        }

        public static bool IsVersionGreatherThan(PackageVersion v1, PackageVersion v2) {
            if (v1.Major.ToIntOrDefault() > v2.Major.ToIntOrDefault()) return true;
            else if (v1.Minor.ToIntOrDefault() > v2.Minor.ToIntOrDefault()) return true;
            else if (v1.Build.ToIntOrDefault() > v2.Build.ToIntOrDefault()) return true;
            else if (v1.Revision1.ToIntOrDefault() > v2.Revision1.ToIntOrDefault()) return true;
            else if (v1.Revision2.ToIntOrDefault() > v2.Revision2.ToIntOrDefault()) return true;
            else if (v1.Revision3.ToIntOrDefault() > v2.Revision3.ToIntOrDefault()) return true;
            else if (v1.Revision4.ToIntOrDefault() > v2.Revision4.ToIntOrDefault()) return true;
            return false;
        }

        public static bool IsVersionSmallerThan(PackageVersion v1, PackageVersion v2) {
            if (v1.Major.ToIntOrDefault() < v2.Major.ToIntOrDefault()) return true;
            else if (v1.Minor.ToIntOrDefault() < v2.Minor.ToIntOrDefault()) return true;
            else if (v1.Build.ToIntOrDefault() < v2.Build.ToIntOrDefault()) return true;
            else if (v1.Revision1.ToIntOrDefault() < v2.Revision1.ToIntOrDefault()) return true;
            else if (v1.Revision2.ToIntOrDefault() < v2.Revision2.ToIntOrDefault()) return true;
            else if (v1.Revision3.ToIntOrDefault() < v2.Revision3.ToIntOrDefault()) return true;
            else if (v1.Revision4.ToIntOrDefault() < v2.Revision4.ToIntOrDefault()) return true;
            return false;
        }

        public static implicit operator PackageVersion(string version) => new(version);

        public static bool operator >(PackageVersion v1, PackageVersion v2) => IsVersionGreatherThan(v1, v2);

        public static bool operator <(PackageVersion v1, PackageVersion v2) => IsVersionSmallerThan(v1, v2);

        public static bool operator ==(PackageVersion v1, PackageVersion v2) {
            if (v1 is null && v2 is null) return true;
            if (v1 is null || v2 is null) return false;
            return v1.ParsedVersion == v2.ParsedVersion;
        }

        public static bool operator !=(PackageVersion v1, PackageVersion v2) {
            return !(v1 == v2);
        }

        public override bool Equals(object obj) {
            if (obj is PackageVersion other) {
                return this.ParsedVersion == other.ParsedVersion;
            }
            return false;
        }

        public override string ToString() {
            return ParsedVersion;
        }

        public override int GetHashCode() {
            return ParsedVersion?.GetHashCode() ?? 0;
        }

        public int CompareTo(PackageVersion other) {
            if (other == null) return 1;

            int result = Major.ToIntOrDefault().CompareTo(other.Major.ToIntOrDefault());
            if (result != 0) return result;

            if (!string.IsNullOrEmpty(Minor) && !string.IsNullOrEmpty(other.Minor)) {
                result = Minor.ToIntOrDefault().CompareTo(other.Minor.ToIntOrDefault());
                if (result != 0) return result;
            }

            if (!string.IsNullOrEmpty(Build) && !string.IsNullOrEmpty(other.Build)) {
                result = Build.ToIntOrDefault().CompareTo(other.Build.ToIntOrDefault());
                if (result != 0) return result;
            }

            if (!string.IsNullOrEmpty(Revision1) && !string.IsNullOrEmpty(other.Revision1)) {
                result = Revision1.ToIntOrDefault().CompareTo(other.Revision1.ToIntOrDefault());
                if (result != 0) return result;
            }

            if (!string.IsNullOrEmpty(Revision2) && !string.IsNullOrEmpty(other.Revision2)) {
                result = Revision2.ToIntOrDefault().CompareTo(other.Revision2.ToIntOrDefault());
                if (result != 0) return result;
            }

            if (!string.IsNullOrEmpty(Revision3) && !string.IsNullOrEmpty(other.Revision3)) {
                result = Revision3.ToIntOrDefault().CompareTo(other.Revision3.ToIntOrDefault());
                if (result != 0) return result;
            }

            if (!string.IsNullOrEmpty(Revision4) && !string.IsNullOrEmpty(other.Revision4)) {
                result = Revision4.ToIntOrDefault().CompareTo(other.Revision4.ToIntOrDefault());
                if (result != 0) return result;
            }

            return result;
        }
    }

    public class PackageVulnerability {
        public string AdvisoryUrl { get; set; }
        public string Severity { get; set; }
        public string Title { get; set; }
        public string CVE { get; set; }
        public List<string> AffectedVersions { get; set; }
    }

    public class PackageFramework {
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string NickName { get; set; }
        public bool AnyVersion => Version.Major == "0";
        public PackageVersion Version {
            get {
                var version = NickName?.Replace("net", "") ?? "0";
                if(version.StartsWith('4') && !version.Contains('.')) {
                    //472, 462, 481
                    var versionParsed = string.Join(".", version.ToArray());
                    return versionParsed;
                }
                return version;
            }
        }

        public override string ToString() {
            return Version.ToString();
        }
    }
}

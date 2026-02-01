using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using wng.Model;

namespace wng.Providers {
    public class NuGetProviderRepository : IDisposable {

        //var tmp1 = NuGetFramework.Parse("net9.0");
        //tmp1.ToString();

        //var tmp2 = NuGetFramework.Parse("v4.8".Replace("v", "net"));
        //tmp2.ToString();

        private readonly ILogger logger = NullLogger.Instance;
        private readonly SourceCacheContext cache = new();
        private readonly SourceRepository repository = Repository.Factory.GetCoreV3(NuGetConstants.V3FeedUrl);

        public async Task<NuGetRepositoryPackageResult> GetNuGetPackageAsync(string name, bool includePreRelease, CancellationToken ct = default) {

            var metadataRepository = await repository.GetResourceAsync<PackageMetadataResource>();
            var metaDataList = await metadataRepository.GetMetadataAsync(
                name.Trim(),
                includePrerelease: includePreRelease,
                includeUnlisted: true,
                cache, logger, ct
            );

            var package = new NuGetRepositoryPackageResult(name);

            foreach (IPackageSearchMetadata metaData in metaDataList) {
                NuGetVersion version;
                if (metaData is PackageSearchMetadataRegistration md) version = md.Version;
                else version = (await metaData.GetVersionsAsync()).FirstOrDefault()?.Version;
                version ??= new NuGetVersion("0.0.0");

                var deprecation = await metaData.GetDeprecationMetadataAsync();
                var ngVersion = new NuGetRepositoryVersionVersion(version, metaData, deprecation);
                package.Versions.Add(ngVersion);
            }

            return package;
        }

        public void Dispose() {
            cache.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    public class NuGetRepositoryPackageResult(string name) {

        public string Name { get; init; } = name;
        public List<NuGetRepositoryVersionVersion> Versions { get; init; } = [];

    }

    public class NuGetRepositoryVersionVersion(NuGetVersion version, IPackageSearchMetadata metaData, PackageDeprecationMetadata deprecation) {

        public NuGetVersion Version { get; init; } = version;
        public IPackageSearchMetadata MetaData { get; init; } = metaData;
        public PackageDeprecationMetadata Deprecation { get; init; } = deprecation;

        public NuGetFramework TargetFramework {
            get {
                if (MetaData.DependencySets?.Any() == true) {
                    var frameworks = MetaData.DependencySets.Select(d => d.TargetFramework).OrderByDescending(d => d.Version).ToList();
                    var netFrameworks = frameworks.Where(f => f.GetShortFolderName().StartsWith("net", StringComparison.OrdinalIgnoreCase)).ToList();
                    return netFrameworks.Count > 0 ? netFrameworks.First() : frameworks.First();
                }
                return null;
            }
        }

        public List<PackageFramework> SupportedFrameworks {
            get {
                var frameworks = new List<PackageFramework>();
                if (MetaData.DependencySets?.Any() == true) {
                    foreach (var depSet in MetaData.DependencySets) {
                        if (depSet.TargetFramework == NuGetFramework.AnyFramework || depSet.TargetFramework.DotNetFrameworkName.Contains("standard", StringComparison.OrdinalIgnoreCase)) {
                            frameworks.Add(new PackageFramework() { Name = "Any Framework", NickName = "Any", ShortName = "net0" });
                        }
                        else if (depSet.TargetFramework.GetShortFolderName().StartsWith("net", StringComparison.OrdinalIgnoreCase)) {
                            var framework = depSet.TargetFramework;
                            var shortName = framework.GetShortFolderName();
                            var nickName = shortName.Replace("core", "").Replace("standard", "");
                            frameworks.Add(new PackageFramework { Name = framework.DotNetFrameworkName, ShortName = shortName, NickName = nickName });
                        }
                    }
                }
                else {
                    //If the dependency sets are empty, assume it supports any framework
                    frameworks.Add(new PackageFramework() { Name = "Any Framework", NickName = "Any", ShortName = "net0" });
                }
                return [.. frameworks.OrderByDescending(f => f.Version)];
            }
        }

        public bool SupportFrameworkVersion(PackageVersion frameworkVersion) {
            if (MetaData.DependencySets?.Any() == true) {
                var nugetFramework = NuGetFramework.Parse($"net{frameworkVersion}");
                foreach (var depSet in MetaData.DependencySets) {
                    if (depSet.TargetFramework == NuGetFramework.AnyFramework) return true;
                    if (NuGetFramework.FrameworkNameComparer.Equals(depSet.TargetFramework, nugetFramework)) {
                        return true;
                    }
                }
            }
            return false;
        }

        public IEnumerable<PackageVulnerabilityMetadata> Vulnerabilities => MetaData?.Vulnerabilities ?? [];
    }
}

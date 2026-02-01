using wng.Model;
using wng.Model.Dotnet;
using wng.Providers;

namespace testing {
    public class NuGetProviderTest {

        private NuGetProvider _nugetProvider;

        private static readonly bool DebugMode = false;
        private static readonly string resourcesFolderPath = "Resources\\NuGet";
        private static readonly string resourcesTempFolderPath = "Resources\\NuGet\\Temp";
        private static readonly Dictionary<string, string> testProjectFiles = [];

        public static readonly CancellationTokenSource cancellationTokenSource = new();
        public static readonly CancellationToken ct = cancellationTokenSource.Token;

        [OneTimeSetUp]
        public void Setup() {
            _nugetProvider = new NuGetProvider(debugMode: DebugMode);
            testProjectFiles.Clear();

            var resourcesFolder = new DirectoryInfo(resourcesFolderPath);
            if (resourcesFolder.Exists) {
                var tempFolder = new DirectoryInfo(resourcesTempFolderPath);
                if (tempFolder.Exists) { tempFolder.Delete(recursive: true); }

                //Copy all files to temp folder, preserving structure
                resourcesFolder.CreateSubdirectory("Temp");
                foreach (var file in resourcesFolder.GetFiles("*", SearchOption.AllDirectories)) {
                    var relativePath = Path.GetRelativePath(resourcesFolder.FullName, file.FullName);
                    var destinationPath = Path.Combine(resourcesTempFolderPath, relativePath);
                    var destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destinationDir) && destinationDir is not null) {
                        Directory.CreateDirectory(destinationDir);
                    }
                    file.CopyTo(destinationPath, overwrite: true);
                    var destinationFile = new FileInfo(destinationPath);
                    testProjectFiles.Add(destinationFile.Name, destinationFile.FullName);
                }
            }
        }

        [OneTimeTearDown]
        public void TearDown() {
            _nugetProvider.Dispose();
            testProjectFiles.Clear();
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            var tempFolder = new DirectoryInfo(resourcesTempFolderPath);
            if (tempFolder.Exists) { tempFolder.Delete(recursive: true); }
        }

        [Test]
        public async Task Test_NuGetProvider_RefreshNuGetPackage() {
            List<NuGetPackage> packages = [
                new NuGetPackage("StackExchange.Redis", "1.2.4"),
                new NuGetPackage("Newtonsoft.Json", "3.5.8"),
                new NuGetPackage("Dapper", "2.0.123"),
            ];

            var minimumFrameworkVersion = new PackageVersion("9.0");

            foreach (var package in packages) {
                var result = await _nugetProvider.RefreshPackageAsync(package, minimumFrameworkVersion, ct: ct);
                Assert.That(result, Is.Not.Null);

                if (package.Name == "Newtonsoft.Json") {
                    using (Assert.EnterMultipleScope()) {
                        Assert.That(package.CurrentVersion.Vulnerabilities, Has.Count.GreaterThan(0));
                        Assert.That(package.CurrentVersion.Vulnerabilities.Any(vuln => vuln.Severity == "2"), Is.True);
                    }
                }

                using (Assert.EnterMultipleScope()) {
                    Assert.That(result.AllVersions, Has.Count.GreaterThan(0));
                    Assert.That(result.LatestVersion?.Major, Is.Not.Null);
                    Assert.That(result.LatestMinorVersion?.Major, Is.Not.Null);
                }
            }
        }

        [Test]
        public async Task Test_NuGetProvider_GetProjectPackages() {

            foreach (var project in testProjectFiles.Keys) {

                var projectFilePath = testProjectFiles[project];
                var projectList = NuGetProvider.GetNuGetPackageProjects(projectFilePath);
                var request = new NuGetPackageRequest(projectList);
                var result = await _nugetProvider.GetNuGetPackagesAsync(request);
                Assert.That(result, Is.Not.Null);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(result.Projects, Has.Count.EqualTo(1));

                    var projectPackages = result.Projects[0];
                    Assert.That(projectPackages.Packages?.ToList(), Has.Count.GreaterThan(0));

                    if (projectPackages.Packages is not null) {
                        foreach (var pkg in projectPackages.Packages) {
                            Assert.That(pkg.Name, Is.Not.Null.And.Not.Empty);
                            Assert.That(pkg.CurrentVersion.Version, Is.Not.Null.And.Not.Empty);
                        }
                    }
                }

            }
        }
    }
}

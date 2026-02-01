using wng;
using wng.Model.Npm;
using wng.Providers;

namespace testing {
    public class NpmProviderTest {

        private NpmProvider _npmProvider;
        private static readonly bool DebugMode = false;

        private static readonly string[] packageJsonFileNames = ["package-1.json", "package-2.json", "package-3.json", "vulnerable\\package.json"];
        private static readonly string packageJsonFolderPath = "Resources\\Npm";
        private static readonly string packageJsonTempFolderPath = "Resources\\Npm\\Temp";

        public static readonly CancellationTokenSource cancellationTokenSource = new();
        public static readonly CancellationToken ct = cancellationTokenSource.Token;

        [OneTimeSetUp]
        public void Setup() {
            _npmProvider = new NpmProvider(debugMode: DebugMode);

            var resourcesFolder = Path.Combine(AppContext.BaseDirectory, packageJsonFolderPath);
            var packageJsonFiles = Directory.GetFiles(resourcesFolder, "package*.json", SearchOption.AllDirectories);

            var tempFolder = Path.Combine(AppContext.BaseDirectory, packageJsonTempFolderPath);
            if (!Directory.Exists(tempFolder)) { Directory.CreateDirectory(tempFolder); }

            foreach (var packageJsonFile in packageJsonFiles) {
                var fileName = Path.GetFileName(packageJsonFile);
                var destFilePath = Path.Combine(tempFolder, fileName);
                File.Copy(packageJsonFile, destFilePath, overwrite: true);
            }

            var vulnerableFolder = Path.Combine(resourcesFolder, "vulnerable");
            var vulnableTempFolder = Path.Combine(tempFolder, "vulnerable");
            if (!Directory.Exists(vulnableTempFolder)) { Directory.CreateDirectory(vulnableTempFolder); }

            var vulnerablePackageJsonFiles = Directory.GetFiles(vulnerableFolder, "package*.json", SearchOption.AllDirectories);
            foreach (var packageJsonFile in vulnerablePackageJsonFiles) {
                var fileName = Path.GetFileName(packageJsonFile);
                var destFilePath = Path.Combine(vulnableTempFolder, fileName);
                File.Copy(packageJsonFile, destFilePath, overwrite: true);
            }
        }

        [OneTimeTearDown]
        public void TearDown() {
            _npmProvider.Dispose();
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            var tempFolder = Path.Combine(AppContext.BaseDirectory, packageJsonTempFolderPath);
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, recursive: true);
        }

        protected static string GetPackageJsonTestFile(string fileName) {
            var tempFolder = Path.Combine(AppContext.BaseDirectory, packageJsonTempFolderPath);
            return Path.Combine(tempFolder, fileName);
        }

        [Test]
        public async Task Test_NPMProvider_ProccessJsonFiles() {
            var packageJsonFiles = packageJsonFileNames.Select(GetPackageJsonTestFile).ToList();

            foreach (var packageJsonFile in packageJsonFiles) {
                var results = await _npmProvider.GetNmpPackagesAsync(new NpmPackageRequest(packageJsonFile), ct);
                Assert.That(results, Is.Not.Null, $"No packages found in file: {packageJsonFile}");

                foreach (var project in results.Projects) {
                    foreach (var package in project.AllDependencies) {
                        using (Assert.EnterMultipleScope()) {
                            Assert.That(package.AllVersions, Has.Count.GreaterThan(0), packageJsonFile);
                            Assert.That(package.LatestVersion?.Major, Is.Not.Null, packageJsonFile);
                            Assert.That(package.LatestMinorVersion?.Major, Is.Not.Null, packageJsonFile);
                        }
                    }
                }
            }
        }

        [Test]
        public async Task Test_NPMProvider_SegmentedSearch() {
            var packageJsonFile = GetPackageJsonTestFile("package-3.json");
            Assert.That(packageJsonFile, Is.Not.Null);

            var results = await _npmProvider.GetNmpPackagesAsync(new NpmPackageRequest(packageJsonFile) {
                InclusivePackages = ["@syncfusion"],
                PrepareAction = p => p.SetCommandSettings(false, 24)
            }, ct);

            Assert.That(results, Is.Not.Null, $"No packages found in file: {packageJsonFile}");

            foreach (var project in results.Projects) {
                foreach (var package in project.AllDependencies) {
                    using (Assert.EnterMultipleScope()) {
                        Assert.That(package.AllVersions, Has.Count.GreaterThan(0), packageJsonFile);
                        Assert.That(package.LatestVersion?.Major, Is.Not.Null, packageJsonFile);
                        Assert.That(package.LatestMinorVersion?.Major, Is.Not.Null, packageJsonFile);
                    }
                }
            }

        }

        [Test]
        public async Task Test_NPMProvider_IgnoredPackages() {
            var packageJsonFile = GetPackageJsonTestFile("package-3.json");
            Assert.That(packageJsonFile, Is.Not.Null);

            var request = new NpmPackageRequest(packageJsonFile) {
                IgnorePackages = ["@angular", "@ngx"],
                PrepareAction = p => p.SetCommandSettings(false, 24)
            };
            var results = await _npmProvider.GetNmpPackagesAsync(request, ct);

            Assert.That(results, Is.Not.Null, $"No packages found in file: {packageJsonFile}");

            var hasIgnoredPackages = results.AllPackages.Any(p => request.IgnorePackages.Any(ig => p.Name.Contains(ig, StringComparison.OrdinalIgnoreCase)));
            Assert.That(hasIgnoredPackages, Is.False, "Ignored packages were found in the results.");

            foreach (var project in results.Projects) {
                foreach (var package in project.AllDependencies) {
                    using (Assert.EnterMultipleScope()) {
                        Assert.That(package.AllVersions, Has.Count.GreaterThan(0), packageJsonFile);
                        Assert.That(package.LatestVersion?.Major, Is.Not.Null, packageJsonFile);
                        Assert.That(package.LatestMinorVersion?.Major, Is.Not.Null, packageJsonFile);
                    }
                }
            }

        }

        [Test]
        public async Task Test_NPMProvider_VersionURL() {
            var packageJsonFile = GetPackageJsonTestFile("package-3.json");
            Assert.That(packageJsonFile, Is.Not.Null);

            var request = new NpmPackageRequest(packageJsonFile) {
                InclusivePackages = ["webpack"]
            };

            var results = await _npmProvider.GetNmpPackagesAsync(request, ct);
            Assert.That(results, Is.Not.Null, $"No packages found in file: {packageJsonFile}");

            foreach (var project in results.Projects) {
                foreach (var package in project.AllDependencies) {
                    var latestVersionUrl = package.LatestVersion.GetUrl(package.PackageUrl);
                    var currentVersionUrl = package.CurrentVersion.GetUrl(package.PackageUrl);
                    var latestMinorVersionUrl = package.LatestMinorVersion.GetUrl(package.PackageUrl);

                    using (Assert.EnterMultipleScope()) {
                        Assert.That(latestVersionUrl, Is.Not.Null);
                        Assert.That(latestVersionUrl, Is.Not.Empty);
                        Assert.That(currentVersionUrl, Is.Not.Null);
                        Assert.That(currentVersionUrl, Is.Not.Empty);
                        Assert.That(latestMinorVersionUrl, Is.Not.Null);
                        Assert.That(latestMinorVersionUrl, Is.Not.Empty);
                    }
                }
            }
        }

        [Test]
        public async Task Test_NPMProvider_ReplaceVersion_Extension() {

            //https://gist.github.com/jonlabelle/706b28d50ba75bf81d40782aa3c84b3e
            List<PackageVersionTestItem> versions = [
                new PackageVersionTestItem("23.1.1", "\"@ckeditor/ckeditor5-ui\": \"23.1.0\",", "\"@ckeditor/ckeditor5-ui\": \"23.1.1\","),
                new PackageVersionTestItem("23.1.1", "\"@ckeditor/ckeditor5-ui\": \"^23.1.0\",", "\"@ckeditor/ckeditor5-ui\": \"^23.1.1\","),
                new PackageVersionTestItem("23.1.1", "\"@ckeditor/ckeditor5-ui\": \"~23.1.0\",", "\"@ckeditor/ckeditor5-ui\": \"~23.1.1\","),
                new PackageVersionTestItem("23.1.1", "\"@ckeditor/ckeditor5-ui\": \">23.1.0\",", "\"@ckeditor/ckeditor5-ui\": \">23.1.1\","),
                new PackageVersionTestItem("23.1.1", "\"@ckeditor/ckeditor5-ui\": \"<23.1.0\",", "\"@ckeditor/ckeditor5-ui\": \"<23.1.1\","),
                new PackageVersionTestItem("23.1.1", "\"@ckeditor/ckeditor5-ui\": \">=23.1.0\",", "\"@ckeditor/ckeditor5-ui\": \">=23.1.1\","),
                new PackageVersionTestItem("23.1.1", "\"@ckeditor/ckeditor5-ui\": \"<=23.1.0\",", "\"@ckeditor/ckeditor5-ui\": \"<=23.1.1\","),
                new PackageVersionTestItem("23.1.1", "\"@ckeditor/ckeditor5-ui\": \"=23.1.0\",", "\"@ckeditor/ckeditor5-ui\": \"=23.1.1\","),
                new PackageVersionTestItem("23.1.1", "\"@ckeditor/ckeditor5-ui\": \"*\",", "\"@ckeditor/ckeditor5-ui\": \"*\","),
                // ---
                new PackageVersionTestItem("21.1.0-alpha.1", "\"@ckeditor/ckeditor5-ui\": \"23.1.0\",", "\"@ckeditor/ckeditor5-ui\": \"21.1.0-alpha.1\","),
                new PackageVersionTestItem("21.1.0-next.4", "\"@ckeditor/ckeditor5-ui\": \"^23.1.0\",", "\"@ckeditor/ckeditor5-ui\": \"^21.1.0-next.4\","),
                new PackageVersionTestItem("16.2.12", "\"@angular/common\": \"^21.1.0-next.4\",", "\"@angular/common\": \"^16.2.12\","),
                new PackageVersionTestItem("20.0.7", "\"@angular/common\": \"^20.1.0-rc.0\",", "\"@angular/common\": \"^20.0.7\","),
                new PackageVersionTestItem("20.0.7", "\"@angular/common\": \"^20.1.0-dev.20260108\",", "\"@angular/common\": \"^20.0.7\","),
                // ---
                new PackageVersionTestItem("8.0.0-alpha.14", "\"rxjs\": \"~7.8.0\",", "\"rxjs\": \"~8.0.0-alpha.14\","),
                new PackageVersionTestItem("7.8.0", "\"rxjs\": \"~8.0.0-alpha.14\",", "\"rxjs\": \"~7.8.0\","),
                // ---
                new PackageVersionTestItem("24.2.7", "\"@syncfusion/ej2-angular-buttons\": \"^24.2.7-ngcc\",", "\"@syncfusion/ej2-angular-buttons\": \"^24.2.7\","),
                new PackageVersionTestItem("24.2.3-ngcc", "\"@syncfusion/ej2-angular-buttons\": \"~24.2.3\",", "\"@syncfusion/ej2-angular-buttons\": \"~24.2.3-ngcc\","),
                // ---
                new PackageVersionTestItem("6.0.0-beta", "\"typescript\": \"~5.3.6\"", "\"typescript\": \"~6.0.0-beta\""),
                new PackageVersionTestItem("6.0.0-rc", "\"typescript\": \"~5.3.6\"", "\"typescript\": \"~6.0.0-rc\""),
                new PackageVersionTestItem("6.0.0-dev.20260108", "    \"typescript\": \"<5.3.6\",", "    \"typescript\": \"<6.0.0-dev.20260108\","),
                new PackageVersionTestItem("6.0.0-nightlybuild1565", "    \"typescript\": \">=5.3.6\"", "    \"typescript\": \">=6.0.0-nightlybuild1565\""),
            ];

            foreach (var version in versions) {
                var updatedLine = version.CurrentLine.ReplaceVersionInPackageText(version.NewVersion);
                Assert.That(updatedLine, Is.EqualTo(version.NewLine),
                    $"Failed to replace version in line:\nCurrent Line: {version.CurrentLine}\nExpected Line: {version.NewLine}\nUpdated Line: {updatedLine}");
            }
        }

        [Test]
        public async Task Test_NPMProvider_UpdateVersion() {
            var jsonFileName = "package-4.json";

            var packageJsonFile = GetPackageJsonTestFile(jsonFileName);
            Assert.That(packageJsonFile, Is.Not.Null);

            var request = new NpmPackageRequest(packageJsonFile);

            var packages = await _npmProvider.GetNmpPackagesAsync(request, ct);
            Assert.That(packages, Is.Not.Null, $"No packages found in file: {packageJsonFile}");

            var updateRequest = new NpmPackageUpdateRequest(packages);
            var updateResult = await NpmProvider.UpdatePackageVersionsAsync(updateRequest, ct);

            using (Assert.EnterMultipleScope()) {
                Assert.That(updateResult, Is.Not.Null);
                Assert.That(updateResult.Failed, Is.False);
                Assert.That(updateResult.UpdatedPackages, Has.Count.GreaterThan(0), "No packages were updated.");
            }

            var updatedPackageJsonFile = GetPackageJsonTestFile(jsonFileName);
            var updatedPackages = await _npmProvider.GetNmpPackagesAsync(request, ct);

            foreach (var project in packages.Projects) {
                foreach (var package in project.AllDependencies) {
                    var updatedPackage = updatedPackages.AllPackages.FirstOrDefault(p => p.Name == package.Name);
                    Assert.That(updatedPackage, Is.Not.Null, $"Updated package '{package.Name}' not found.");
                    using (Assert.EnterMultipleScope()) {

                        Assert.That(updatedPackage.CurrentVersion, Is.Not.Null,
                            $"Current version for package '{package.Name}' is null.");

                        var updatedVersionMatch = updatedPackage.CurrentVersion.Version == package.DesiredVersion.Version;
                        Assert.That(updatedVersionMatch, Is.True,
                            $"Package '{package.Name}' was not updated to the desired version. Current: {updatedPackage.CurrentVersion}, Expected: {package.LatestVersion}");
                    }
                }
            }
        }

        [Test]
        public async Task Test_NPMProvider_CheckVulnerabilities() {
            var packageJsonFile = GetPackageJsonTestFile("vulnerable\\package.json");
            Assert.That(packageJsonFile, Is.Not.Null);

            var npmAudit = await NpmProvider.GetNpmAuditReportAsync(packageJsonFile, ct);
            Assert.That(npmAudit, Is.Not.Null, "Failed to deserialize NPM audit JSON.");
            Assert.That(npmAudit.Vulnerabilities, Is.Not.Null, "Vulnerabilities section is null.");
            Assert.That(npmAudit.Vulnerabilities, Is.Not.Empty, "No vulnerabilities found in the audit report.");
        }

        [Test]
        public async Task Test_NPMProvider_TestTempFolder_DoNotCommit() {
            var path = "C:\\Temp\\wng-cli\\normal";
            var result = _npmProvider.GetNmpPackagesAsync(new NpmPackageRequest(path), ct);
            Assert.That(result, Is.Not.Null);

            foreach(var project in result.Result.Projects) {
                foreach(var package in project.AllDependencies) {
                    using (Assert.EnterMultipleScope()) {
                        Assert.That(package.AllVersions, Has.Count.GreaterThan(0), path);
                        Assert.That(package.LatestVersion?.Major, Is.Not.Null, path);
                        Assert.That(package.LatestMinorVersion?.Major, Is.Not.Null, path);
                    }
                }
            }
        }
    }

    internal class PackageVersionTestItem(string newVersion, string line, string newLine) {
        public string NewVersion { get; set; } = newVersion;
        public string CurrentLine { get; set; } = line;
        public string NewLine { get; set; } = newLine;
    }
}

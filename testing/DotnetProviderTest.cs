using wng.Model.Dotnet;
using wng.Providers;

namespace testing {
    public class DotnetProviderTest {

        private DotnetProvider dotnetProvider;

        private static readonly bool DebugMode = true;
        public static readonly CancellationTokenSource cancellationTokenSource = new();
        public static readonly CancellationToken ct = cancellationTokenSource.Token;

        private static readonly string resourcesFolderPath = "Resources\\NuGet";
        private static readonly string resourcesTempFolderPath = "Resources\\DotNet\\Temp";
        private static readonly Dictionary<string, string> testProjectFiles = [];

        [OneTimeSetUp]
        public void Setup() {
            dotnetProvider = new DotnetProvider(debugMode: DebugMode);
            testProjectFiles.Clear();

            var resourcesFolder = new DirectoryInfo(resourcesFolderPath);
            if (resourcesFolder.Exists) {
                var tempFolder = new DirectoryInfo(resourcesTempFolderPath);
                if (tempFolder.Exists) { tempFolder.Delete(recursive: true); }
                tempFolder.Create();

                //Copy all files to temp folder, preserving structure
                foreach (var file in resourcesFolder.GetFiles("*.*proj.txt", SearchOption.AllDirectories)) {
                    var relativePath = Path.GetRelativePath(resourcesFolder.FullName, file.FullName);
                    var destinationPath = Path.Combine(resourcesTempFolderPath, relativePath).Replace(".txt", "");
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
            dotnetProvider.Dispose();
            testProjectFiles.Clear();
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            var tempFolder = new DirectoryInfo(resourcesTempFolderPath);
            if (tempFolder.Exists) { tempFolder.Delete(recursive: true); }
        }

        [Test]
        public async Task Test_DotnetProvider_GetDotNetProjects() {

            var resourcesFolder = new DirectoryInfo(resourcesTempFolderPath);
            var solutionPath = resourcesFolder.FullName;

            var dotNetProjects = await dotnetProvider.GetDotNetProjects(solutionPath, ct);
            Assert.That(dotNetProjects.Projects, Is.Not.Null);
            Assert.That(dotNetProjects.Projects.ToList(), Has.Count.GreaterThan(0));

            foreach (var project in dotNetProjects.Projects) {
                var expectedFilePath = testProjectFiles[project.Name];

                using (Assert.EnterMultipleScope()) {
                    Assert.That(testProjectFiles.ContainsKey(project.Name), Is.True, $"Project '{project.Name}' was not expected.");
                    Assert.That(project.FilePath, Is.EqualTo(expectedFilePath), $"Project '{project.Name}' has unexpected file path.");
                    Assert.That(project.FrameworkVersion, Is.Not.Empty, $"Project '{project.Name}' has empty framework version.");
                }
            }
        }

        [Test]
        public async Task Test_DotnetProvider_GetDotNetVersions() {

            var dotnetVersions = await dotnetProvider.GetDotNetVersionsAsync(ct);
            using (Assert.EnterMultipleScope()) {
                Assert.That(dotnetVersions?.NetCore, Is.Not.Null);
                Assert.That(dotnetVersions.NetCore.Versions, Has.Count.GreaterThan(0));
            }
        }

        [Test]
        public async Task Test_DotnetProvider_GetInstalledVersions() {

            var installedVersions = await DotnetProvider.GetInstalledDotnetVersionsAsync(ct);
            using (Assert.EnterMultipleScope()) {
                Assert.That(installedVersions, Is.Not.Null);
                Assert.That(installedVersions.Failed, Is.False);
                Assert.That(installedVersions?.IsInstalled, Is.True);
                Assert.That(installedVersions.Sdks, Has.Count.GreaterThan(0));
            }
        }
    }
}

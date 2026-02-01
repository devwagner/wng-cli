using wng.Providers;

namespace testing {
    public class NuGetRepositoryTest {

        private NuGetProviderRepository _repository;
        public static readonly CancellationTokenSource cancellationTokenSource = new();
        public static readonly CancellationToken ct = cancellationTokenSource.Token;

        [OneTimeSetUp]
        public void Setup() {
            _repository = new NuGetProviderRepository();
        }

        [OneTimeTearDown]
        public void TearDown() {
            _repository.Dispose();
        }

        [Test]
        public async Task Test_NuGetRepository_GetPackage() {

            var package = await _repository.GetNuGetPackageAsync("System.Diagnostics.Contracts ", false, ct);
            Assert.That(package.Versions, Has.Count.GreaterThan(0));

        }
    }
}

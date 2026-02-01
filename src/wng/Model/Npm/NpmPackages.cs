using wng.Model.Dotnet;

namespace wng.Model.Npm {

    public class NpmPackage : Package {

        public NpmPackage(string name, string version)
            : base(name, version, PackageSource.Npm) { }

        public NpmPackage(KeyValuePair<string, string> dependency, int order = 0)
            : base(dependency, PackageSource.Npm, order) { }
    }

    public class NpmPackages {

        public NpmPackages() { }
        public NpmPackages(IEnumerable<NpmPackageProject> projects) => Projects = [.. projects];

        public List<NpmPackageProject> Projects { get; init; } = [];

        public List<NpmPackage> AllPackages => [.. Projects.SelectMany(p => p.AllDependencies)];

        public NpmPackageProject PrimaryPackageJson => Projects.OrderBy(p => p.FilePath.Length).FirstOrDefault();

        public void AddProject(string name, string filePath, IEnumerable<NpmPackage> packages, IEnumerable<NpmPackage> devPackages) {
            var project = new NpmPackageProject(name, filePath, packages, devPackages);
            this.AddProject(project);
        }

        public void AddProject(NpmPackageProject project) {
            if (project.IsValid) Projects.Add(project);
        }

        public void AddProjects(IEnumerable<NpmPackageProject> projects) {
            Projects.AddRange(projects.Where(p => p.IsValid));
        }

        public void ReplaceAllProjects(IEnumerable<NpmPackageProject> projects) {
            Projects.Clear();
            Projects.AddRange(projects.Where(p => p.IsValid));
        }

        public void UpdatePackageProjectsNames() {
            foreach (var project in Projects) {
                project.UpdatePackageProjectName();
            }
        }

        public void Filter(IEnumerable<string> inclusivePackages, IEnumerable<string> ignorePackages) {
            var updatedProjectList = new List<NpmPackageProject>();
            foreach (var project in Projects) {
                updatedProjectList.Add(project.Filter(inclusivePackages, ignorePackages));
            }
            ReplaceAllProjects(updatedProjectList);
        }
    }

    public struct NpmPackageProject {

        public NpmPackageProject() {
            Name = string.Empty;
            FilePath = string.Empty;
            Dependencies = [];
            DevDependencies = [];
            IsValid = false;
        }

        public NpmPackageProject(string name, string filePath, IEnumerable<NpmPackage> packages, IEnumerable<NpmPackage> devPackages) {
            Name = name;
            FilePath = filePath;
            Dependencies = [.. packages];
            DevDependencies = [.. devPackages];
            AllDependencies = [.. packages, .. devPackages];
            IsValid = !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(filePath) && (packages?.Any() ?? false);
        }

        public string Name { get; init; }

        public string FilePath { get; init; }

        public bool IsValid { get; init; } = false;

        public IEnumerable<NpmPackage> Dependencies { get; private set; } = [];

        public IEnumerable<NpmPackage> DevDependencies { get; private set; } = [];

        public readonly string FileName => Path.GetFileName(FilePath);

        public readonly string ProjectPath => PackageJsonFolder.FullName;

        public readonly FileInfo PackageJsonFile => new(FilePath);

        public readonly DirectoryInfo PackageJsonFolder => PackageJsonFile.Directory;

        public IEnumerable<NpmPackage> AllDependencies { get; private set; } = [];

        public readonly void UpdatePackageProjectName() {
            foreach (var package in AllDependencies) {
                package.SetProjectName(Name);
            }
        }

        public NpmPackageProject Filter(IEnumerable<string> inclusivePackages, IEnumerable<string> ignorePackages) {
            if (inclusivePackages.Any()) {
                Dependencies = [.. Dependencies.Where(p => inclusivePackages.Any(name => p.Name.Contains(name, StringComparison.InvariantCultureIgnoreCase)))];
                DevDependencies = [.. DevDependencies.Where(p => inclusivePackages.Any(name => p.Name.Contains(name, StringComparison.InvariantCultureIgnoreCase)))];
            }
            if (ignorePackages.Any()) {
                Dependencies = [.. Dependencies.Where(p => !ignorePackages.Any(ip => p.Name.Contains(ip, StringComparison.InvariantCultureIgnoreCase)))];
                DevDependencies = [.. DevDependencies.Where(p => !ignorePackages.Any(ip => p.Name.Contains(ip, StringComparison.InvariantCultureIgnoreCase)))];
            }
            AllDependencies = [.. Dependencies, .. DevDependencies];
            return this;
        }
    }

    public class NpmPackageRequest(string packageJsonFileOrFolderPath) {
        public string PackageJsonFileOrFolderPath { get; set; } = packageJsonFileOrFolderPath;
        public bool IncludePreRelease { get; set; } = false;
        public IEnumerable<string> InclusivePackages { get; set; } = [];
        public IEnumerable<string> IgnorePackages { get; set; } = [];
        public Action<NpmPackage> PrepareAction { get; set; }
    }

    public class NpmPackageUpdateRequest(NpmPackages projectList) {
        public NpmPackages ProjectList { get; set; } = projectList;
        public bool KeepMajor { get; set; } = false;
        public bool ConsiderRequestedMajor { get; set; } = false;
    }

    public class NpmPackageUpdateResult {
        public List<NpmPackageUpdateResultItem> UpdatedPackages { get; set; } = [];
        public bool Failed => UpdatedPackages.Any(p => p.Failed);
        public void Add(NpmPackage package, bool updated, bool failed, string message = null) {
            UpdatedPackages.Add(new NpmPackageUpdateResultItem {
                Package = package,
                Updated = updated,
                Failed = failed,
                Message = message
            });
        }
    }

    public class NpmPackageUpdateResultItem {
        public NpmPackage Package { get; set; }
        public bool Updated { get; set; }
        public bool Failed { get; set; }
        public string Message { get; set; }
    }

    public class NpmCommandRequest {
        public string Command { get; set; }
        public string PackageJsonFilePath { get; set; }
        public Action<bool, string> NpmLocationAction { get; set; }
        public Action<string> OnDataOutput { get; set; }
        public Action<string> OnErrorOutput { get; set; }
    }

    public readonly struct NpmProjectFile(string name, string filePath) {
        public string Name { get; init; } = name;
        public string FilePath { get; init; } = filePath;

        public NpmPackageProject GetProject() {

            if (string.IsNullOrEmpty(FilePath) || string.IsNullOrEmpty(Name))
                throw new ArgumentException("FilePath and Name must be provided to load the project.");

            var content = Extensions.GetFileContents(FilePath);
            if (string.IsNullOrEmpty(content))
                throw new ArgumentException($"Could not read content from file: {FilePath}");

            var packageJson = System.Text.Json.JsonSerializer.Deserialize<PackageJson>(content) ??
                throw new ArgumentException($"Could not deserialize content from file: {FilePath}");

            var dependencies = packageJson.Dependencies.Select((pkg, index) => new NpmPackage(pkg, index));
            var devDependencies = packageJson.DevDependencies.Select((pkg, index) => new NpmPackage(pkg, index));

            var project = new NpmPackageProject(Name, FilePath, dependencies, devDependencies);
            project.UpdatePackageProjectName();
            return project;
        }
    }
}

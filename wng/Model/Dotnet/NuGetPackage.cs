namespace wng.Model.Dotnet {

    public class NuGetPackage : Package {

        public NuGetPackage(string name, string version)
            : base(name, version, PackageSource.NuGet) { }

        public NuGetPackage(KeyValuePair<string, string> dependency, int order = 0)
            : base(dependency, PackageSource.NuGet, order) { }

    }

    public class NuGetPackages {

        public NuGetPackages() { }
        public NuGetPackages(IEnumerable<DotNetProject> projects) => Projects = [.. projects];

        public List<DotNetProject> Projects { get; init; } = [];
        public List<NuGetPackage> AllPackages => [.. Projects.SelectMany(p => p.Packages)];

        public void AddProject(string name, string filePath, IEnumerable<NuGetPackage> packages) {
            var project = new DotNetProject(name, filePath, packages);
            this.AddProject(project);
        }

        public void AddProject(DotNetProject project) {
            if (project.IsValid) Projects.Add(project);
        }

        public void AddProjects(IEnumerable<DotNetProject> projects) {
            Projects.AddRange(projects.Where(p => p.IsValid));
        }

        public void ReplaceAllProjects(IEnumerable<DotNetProject> projects) {
            Projects.Clear();
            Projects.AddRange(projects.Where(p => p.IsValid));
        }

        public void UpdatePackageProjectsNames() {
            foreach (var project in Projects) {
                project.UpdatePackageProjectName();
            }
        }

        public void Filter(IEnumerable<string> inclusivePackages, IEnumerable<string> ignorePackages) {
            var updatedProjectList = new List<DotNetProject>();
            foreach (var project in Projects) {
                updatedProjectList.Add(project.Filter(inclusivePackages, ignorePackages));
            }
            ReplaceAllProjects(updatedProjectList);
        }
    }

    public class NuGetPackageUpdateRequest(NuGetPackages projectList) {
        public NuGetPackages ProjectList { get; set; } = projectList;
        public bool KeepMajor { get; set; } = false;
        public bool ConsiderRequestedMajor { get; set; } = false;
    }

    public class NuGetPackageUpdateResult {
        public List<NuGetPackageUpdateResultItem> UpdatedPackages { get; set; } = [];
        public bool Failed => UpdatedPackages.Any(p => p.Failed);
        public void Add(NuGetPackage package, bool updated, bool failed, string message = null) {
            UpdatedPackages.Add(new NuGetPackageUpdateResultItem {
                Package = package,
                Updated = updated,
                Failed = failed,
                Message = message
            });
        }
    }

    public class NuGetPackageUpdateResultItem {
        public NuGetPackage Package { get; set; }
        public bool Updated { get; set; }
        public bool Failed { get; set; }
        public string Message { get; set; }
    }


    public class NuGetPackageRequest(NuGetPackages projectList) {
        public NuGetPackages ProjectList { get; set; } = projectList;
        public PackageVersion MinimumFrameworkVersion { get; set; }
        public bool IncludePreRelease { get; set; } = false;
        public IEnumerable<string> InclusivePackages { get; set; }
        public IEnumerable<string> IgnorePackages { get; set; } = [];
        public Action<NuGetPackage> PrepareAction { get; set; }
    }

    public readonly struct NuGetProjectFile(string name, string filePath) {

        public string Name { get; init; } = name;
        public string FilePath { get; init; } = filePath;

        public DotNetProject GetProject() {
            if (string.IsNullOrEmpty(FilePath) || string.IsNullOrEmpty(Name))
                throw new ArgumentException("FilePath and Name must be provided to load the project.");

            var content = Extensions.GetFileContents(FilePath);
            if (string.IsNullOrEmpty(content))
                throw new ArgumentException($"Could not read content from file: {FilePath}");

            var projectPackageList = content.GetNuGetPackageReferences();
            if (!(projectPackageList?.Count > 0)) return default;

            var projectPackages = projectPackageList
                .Select((pkg, index) => new NuGetPackage(pkg, index));

            var project = new DotNetProject(Name, FilePath, projectPackages);
            project.UpdatePackageProjectName();
            return project;
        }
    }
}

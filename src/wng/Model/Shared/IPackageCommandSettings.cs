namespace wng.Model.Shared {

    public interface IBaseCommandSettings {
        string Path { get; }
        bool Silent { get; }
        bool Debug { get; }
    }

    internal interface IPackageCommandSettings : IBaseCommandSettings {
        string PackageNames { get; }
        string IgnorePackageNames { get; }
        int? Major { get; }
        bool Minor { get; }
        bool Install { get; }
        bool IncludePreRelease { get; }
        bool ShowUrl { get; }
        bool ShowProjectUrl { get; }
        bool ShowVersionUrl { get; }
        bool Update { get; }

        List<string> InclusivePackageList { get; }
        List<string> IgnorePackageList { get; }
    }

    public interface IFrameworkCommand : IBaseCommandSettings {
        string ProjectNames { get; }
        string Install { get; }
        string Update { get; }
    }


}

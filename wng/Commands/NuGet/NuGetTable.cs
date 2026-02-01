using wng.Model.Dotnet;
using wng.Model.Shared;

namespace wng.Commands.NuGet {
    internal class NuGetPackageTable(NuGetCommand.Settings settings, List<NuGetPackage> packages, List<NuGetPackage> allPackages, string section, bool showProjectColumn)
        : PackageTable<NuGetPackage, NuGetCommand.Settings>(settings, packages, allPackages, section, showProjectColumn, false) {

        public static void Render(NuGetCommand.Settings settings, List<NuGetPackage> packages, List<NuGetPackage> allPackages, string section, bool showProjectColumn) {
            var table = new NuGetPackageTable(settings, packages, allPackages, section, showProjectColumn);
            table.Render();
        }
    }
}
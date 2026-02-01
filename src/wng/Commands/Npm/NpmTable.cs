using wng.Model.Npm;
using wng.Model.Shared;

namespace wng.Commands.Npm {
    internal class NpmPackageTable(NpmCommand.Settings settings, List<NpmPackage> packages, List<NpmPackage> allPackages, string section, bool showProjectColumn) 
        : PackageTable<NpmPackage, NpmCommand.Settings>(settings, packages, allPackages, section, showProjectColumn) {

        public static void Render(NpmCommand.Settings settings, List<NpmPackage> packages, List<NpmPackage> allPackages, string section, bool showProjectColumn) {
            var table = new NpmPackageTable(settings, packages, allPackages, section, showProjectColumn);
            table.Render();
        }
    }
}
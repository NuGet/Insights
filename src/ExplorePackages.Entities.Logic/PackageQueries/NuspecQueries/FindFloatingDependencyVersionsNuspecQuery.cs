using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindFloatingDependencyVersionsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindFloatingDependencyVersionsNuspecQuery;
        public string CursorName => CursorNames.FindFloatingDependencyVersionsNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetFloatingDependencyVersions(nuspec)
                .Any();
        }
    }
}

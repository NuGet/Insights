using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindMissingDependencyVersionsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindMissingDependencyVersionsNuspecQuery;
        public string CursorName => CursorNames.FindMissingDependencyVersionsNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetMissingDependencyVersions(nuspec)
                .Any();
        }
    }
}

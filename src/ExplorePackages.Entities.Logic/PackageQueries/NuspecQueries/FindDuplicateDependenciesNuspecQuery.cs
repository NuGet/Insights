using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindDuplicateDependenciesNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindDuplicateDependenciesNuspecQuery;
        public string CursorName => CursorNames.FindDuplicateDependenciesNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetDuplicateDependencies(nuspec)
                .Any();
        }
    }
}

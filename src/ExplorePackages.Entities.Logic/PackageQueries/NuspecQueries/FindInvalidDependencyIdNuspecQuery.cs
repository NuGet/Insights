using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindInvalidDependencyIdNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindInvalidDependencyIdNuspecQuery;
        public string CursorName => CursorNames.FindInvalidDependencyIdNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetInvalidDependencyIds(nuspec)
                .Any();
        }
    }
}

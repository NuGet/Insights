using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindMissingDependencyIdsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindMissingDependencyIdsNuspecQuery;
        public string CursorName => CursorNames.FindMissingDependencyIdsNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetMissingDependencyIds(nuspec)
                .Any();
        }
    }
}

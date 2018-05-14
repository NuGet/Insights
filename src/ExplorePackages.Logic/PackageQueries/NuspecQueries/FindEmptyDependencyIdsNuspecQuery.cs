using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindEmptyDependencyIdsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindEmptyDependencyIdsNuspecQuery;
        public string CursorName => CursorNames.FindEmptyDependencyIdsNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetEmptyDependencyIds(nuspec)
                .Any();
        }
    }
}

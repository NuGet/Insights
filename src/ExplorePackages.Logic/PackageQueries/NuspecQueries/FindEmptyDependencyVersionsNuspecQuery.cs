using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindEmptyDependencyVersionsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindEmptyDependencyVersionsNuspecQuery;
        public string CursorName => CursorNames.FindEmptyDependencyVersionsNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetEmptyDependencyVersions(nuspec)
                .Any();
        }
    }
}

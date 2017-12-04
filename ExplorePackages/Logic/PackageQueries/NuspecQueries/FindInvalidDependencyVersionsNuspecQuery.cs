using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindInvalidDependencyVersionsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindInvalidDependencyVersionsNuspecQuery;
        public string CursorName => CursorNames.FindInvalidDependencyVersionsNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetInvalidDependencyVersions(nuspec)
                .Any();
        }
    }
}

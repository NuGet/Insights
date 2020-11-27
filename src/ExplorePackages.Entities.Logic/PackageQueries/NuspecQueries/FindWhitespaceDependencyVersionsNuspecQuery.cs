using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindWhitespaceDependencyVersionsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindWhitespaceDependencyVersionsNuspecQuery;
        public string CursorName => CursorNames.FindWhitespaceDependencyVersionsNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetWhitespaceDependencyVersions(nuspec)
                .Any();
        }
    }
}

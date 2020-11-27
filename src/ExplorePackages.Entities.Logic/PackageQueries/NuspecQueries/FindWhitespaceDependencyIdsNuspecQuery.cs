using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindWhitespaceDependencyIdsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindWhitespaceDependencyIdsNuspecQuery;
        public string CursorName => CursorNames.FindWhitespaceDependencyIdsNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetWhitespaceDependencyIds(nuspec)
                .Any();
        }
    }
}

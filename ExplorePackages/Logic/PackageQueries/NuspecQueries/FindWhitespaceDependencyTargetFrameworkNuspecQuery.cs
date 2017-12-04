using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindWhitespaceDependencyTargetFrameworkNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindWhitespaceDependencyTargetFrameworkNuspecQuery;
        public string CursorName => CursorNames.FindWhitespaceDependencyTargetFrameworkNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetWhitespaceDependencyTargetFrameworks(nuspec)
                .Any();
        }
    }
}

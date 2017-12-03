using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindUnsupportedDependencyTargetFrameworkNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindUnsupportedDependencyTargetFrameworkNuspecQuery;
        public string CursorName => CursorNames.FindUnsupportedDependencyTargetFrameworkNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetUnsupportedDependencyTargetFrameworks(nuspec)
                .Any();
        }
    }
}

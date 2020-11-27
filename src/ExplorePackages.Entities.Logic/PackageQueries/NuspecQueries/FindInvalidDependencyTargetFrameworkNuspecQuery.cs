using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindInvalidDependencyTargetFrameworkNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindInvalidDependencyTargetFrameworkNuspecQuery;
        public string CursorName => CursorNames.FindInvalidDependencyTargetFrameworkNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetInvalidDependencyTargetFrameworks(nuspec)
                .Any();
        }
    }
}

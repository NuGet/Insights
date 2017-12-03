using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindDuplicateNormalizedDependencyTargetFrameworksNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindDuplicateNormalizedDependencyTargetFrameworksNuspecQuery;
        public string CursorName => CursorNames.FindDuplicateNormalizedDependencyTargetFrameworksNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetDuplicateNormalizedDependencyTargetFrameworks(nuspec)
                .Any();
        }
    }
}

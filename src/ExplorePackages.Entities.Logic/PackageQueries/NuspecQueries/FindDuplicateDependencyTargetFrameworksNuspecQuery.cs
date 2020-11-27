using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindDuplicateDependencyTargetFrameworksNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindDuplicateDependencyTargetFrameworksNuspecQuery;
        public string CursorName => CursorNames.FindDuplicateDependencyTargetFrameworksNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetDuplicateDependencyTargetFrameworks(nuspec)
                .Any();
        }
    }
}

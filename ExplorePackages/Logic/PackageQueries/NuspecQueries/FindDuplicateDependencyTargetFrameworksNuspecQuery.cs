using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindDuplicateDependencyTargetFrameworksNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindDuplicateDependencyTargetFrameworksNuspecQuery;
        public string CursorName => CursorNames.FindDuplicateDependencyTargetFrameworksNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetDependencyTargetFrameworks(nuspec)
                .Select(x => string.IsNullOrEmpty(x) ? string.Empty : x)
                .GroupBy(x => x)
                .Where(x => x.Count() > 2)
                .Any();
        }
    }
}

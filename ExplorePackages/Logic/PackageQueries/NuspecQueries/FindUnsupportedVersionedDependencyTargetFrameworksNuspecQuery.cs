using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindUnsupportedVersionedDependencyTargetFrameworksNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindUnsupportedVersionedDependencyTargetFrameworksNuspecQuery;
        public string CursorName => CursorNames.FindUnsupportedVersionedDependencyTargetFrameworksNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetVersionedUnsupportedDependencyTargetFrameworks(nuspec)
                .Any();
        }
    }
}

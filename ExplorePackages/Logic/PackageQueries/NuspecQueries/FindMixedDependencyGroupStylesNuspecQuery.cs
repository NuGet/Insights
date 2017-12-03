using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindMixedDependencyGroupStylesNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindMixedDependencyGroupStylesNuspecQuery;
        public string CursorName => CursorNames.FindMixedDependencyGroupStylesNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility.HasMixedDependencyGroupStyles(nuspec);
        }
    }
}

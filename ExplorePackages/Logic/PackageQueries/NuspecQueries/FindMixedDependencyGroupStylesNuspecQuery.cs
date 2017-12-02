using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindMixedDependencyGroupStylesNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindMixedDependencyGroupStylesNuspecQuery;
        public string CursorName => CursorNames.FindMixedDependencyGroupStylesNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            var groups = NuspecUtility.GetDependencyGroups(nuspec);

            return groups.Dependencies.Any() && groups.Groups.Any();
        }
    }
}

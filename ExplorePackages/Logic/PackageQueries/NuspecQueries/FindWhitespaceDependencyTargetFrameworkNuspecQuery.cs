using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindWhitespaceDependencyTargetFrameworkNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindWhitespaceDependencyTargetFrameworkNuspecQuery;
        public string CursorName => CursorNames.FindWhitespaceDependencyTargetFrameworkNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            var groups = NuspecUtility.GetDependencyGroups(nuspec);
            foreach (var group in groups.Groups)
            {
                if (!string.IsNullOrEmpty(group.TargetFramework)
                    && string.IsNullOrWhiteSpace(group.TargetFramework))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

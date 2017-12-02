using System.Xml.Linq;
using NuGet.Frameworks;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindInvalidDependencyTargetFrameworkNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindInvalidDependencyTargetFrameworkNuspecQuery;
        public string CursorName => CursorNames.FindInvalidDependencyTargetFrameworkNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            var groups = NuspecUtility.GetDependencyGroups(nuspec);

            foreach (var group in groups.Groups)
            {
                try
                {
                    NuGetFramework.Parse(group.TargetFramework);
                }
                catch
                {
                    return true;
                }
            }

            return false;
        }
    }
}

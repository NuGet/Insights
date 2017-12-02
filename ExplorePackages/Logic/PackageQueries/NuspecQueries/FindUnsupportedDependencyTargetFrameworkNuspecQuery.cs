using System.Xml.Linq;
using NuGet.Frameworks;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindUnsupportedDependencyTargetFrameworkNuspecQuery : INuspecQuery
    {
        private static readonly NuGetFrameworkNameComparer Compararer = new NuGetFrameworkNameComparer();

        public string Name => PackageQueryNames.FindUnsupportedDependencyTargetFrameworkNuspecQuery;
        public string CursorName => CursorNames.FindUnsupportedDependencyTargetFrameworkNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            var groups = NuspecUtility.GetDependencyGroups(nuspec);

            foreach (var group in groups.Groups)
            {
                if (string.IsNullOrWhiteSpace(group.TargetFramework))
                {
                    continue;
                }

                try
                {
                    var parsedFramework = NuGetFramework.Parse(group.TargetFramework);
                    if (Compararer.Equals(parsedFramework, NuGetFramework.UnsupportedFramework))
                    {
                        return true;
                    }
                }
                catch
                {
                    continue;
                }
            }

            return false;
        }
    }
}

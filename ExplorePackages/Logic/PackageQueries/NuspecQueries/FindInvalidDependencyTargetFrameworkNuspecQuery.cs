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
            foreach (var targetFramework in NuspecUtility.GetDependencyTargetFrameworks(nuspec))
            {
                if (string.IsNullOrWhiteSpace(targetFramework))
                {
                    continue;
                }

                try
                {
                    NuGetFramework.Parse(targetFramework);
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

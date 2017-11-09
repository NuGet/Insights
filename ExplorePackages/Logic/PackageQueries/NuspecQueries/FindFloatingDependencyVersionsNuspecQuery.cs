using System.Xml.Linq;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindFloatingDependencyVersionsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindFloatingDependencyVersionsNuspecQuery;
        public string CursorName => CursorNames.FindFloatingDependencyVersionsNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            var dependencyEls = NuspecUtility.GetDependencies(nuspec);

            foreach (var dependencyEl in dependencyEls)
            {
                var version = dependencyEl.Attribute("version")?.Value;
                if (!string.IsNullOrEmpty(version)
                    && VersionRange.TryParse(version, out var parsed)
                    && parsed.IsFloating)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

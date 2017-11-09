using System.Xml.Linq;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindInvalidDependencyVersionsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindInvalidDependencyVersionsNuspecQuery;
        public string CursorName => CursorNames.FindInvalidDependencyVersionsNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            var dependencyEls = NuspecUtility.GetDependencies(nuspec);

            foreach (var dependencyEl in dependencyEls)
            {
                var version = dependencyEl.Attribute("version")?.Value;
                if (!string.IsNullOrEmpty(version)
                    && !VersionRange.TryParse(version, out var parsed))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

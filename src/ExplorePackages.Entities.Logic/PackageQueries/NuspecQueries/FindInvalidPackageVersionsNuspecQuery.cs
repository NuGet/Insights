using System.Xml.Linq;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindInvalidPackageVersionsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindInvalidPackageVersionsNuspecQuery;
        public string CursorName => CursorNames.FindInvalidPackageVersionsNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            var originalVersion = NuspecUtility.GetOriginalVersion(nuspec);
            if (!NuGetVersion.TryParse(originalVersion, out var parsedVersion))
            {
                return true;
            }

            return false;
        }
    }
}

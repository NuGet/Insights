using System.Xml.Linq;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindInvalidPackageVersionsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindInvalidPackageVersionsNuspecQuery;
        public string CursorName => CursorNames.FindInvalidPackageVersionsNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            var orignalVersion = NuspecUtility.GetOriginalVersion(nuspec);
            if (!NuGetVersion.TryParse(orignalVersion, out var parsedVersion))
            {
                return true;
            }

            return false;
        }
    }
}

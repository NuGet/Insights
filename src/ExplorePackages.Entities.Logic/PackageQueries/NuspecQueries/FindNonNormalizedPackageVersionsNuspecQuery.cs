using System;
using System.Xml.Linq;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindNonNormalizedPackageVersionsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindNonNormalizedPackageVersionsNuspecQuery;
        public string CursorName => CursorNames.FindNonNormalizedPackageVersionsNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            var originalVersion = NuspecUtility.GetOriginalVersion(nuspec);
            if (NuGetVersion.TryParse(originalVersion, out var parsedVersion))
            {
                var normalizedVersion = parsedVersion.ToFullString();
                return !StringComparer.OrdinalIgnoreCase.Equals(originalVersion, normalizedVersion);
            }

            return false;
        }
    }
}

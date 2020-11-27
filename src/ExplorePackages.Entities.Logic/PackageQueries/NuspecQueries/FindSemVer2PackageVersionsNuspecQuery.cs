using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindSemVer2PackageVersionsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindSemVer2PackageVersionsNuspecQuery;
        public string CursorName => CursorNames.FindSemVer2PackageVersionsNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility.HasSemVer2PackageVersion(nuspec);
        }
    }
}

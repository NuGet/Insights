using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindSemVer2DependencyVersionsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindSemVer2DependencyVersionsNuspecQuery;
        public string CursorName => CursorNames.FindSemVer2DependencyVersionsNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility.HasSemVer2DependencyVersion(nuspec);
        }
    }
}

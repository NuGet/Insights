using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindInvalidPackageIdsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindInvalidPackageIdsNuspecQuery;
        public string CursorName => CursorNames.FindInvalidPackageIdsNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            var id = NuspecUtility.GetOriginalId(nuspec);
            return !StrictPackageIdValidator.IsValid(id);
        }
    }
}

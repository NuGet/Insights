using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindCaseInsensitiveDuplicateMetadataElementsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindCaseInsensitiveDuplicateMetadataElementsNuspecQuery;
        public string CursorName => CursorNames.FindCaseInsensitiveDuplicateMetadataElementsNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetDuplicateMetadataElements(nuspec, caseSensitive: false, onlyText: false)
                .Any();
        }
    }
}

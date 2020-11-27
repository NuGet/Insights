using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindCaseInsensitiveDuplicateTextMetadataElementsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindCaseInsensitiveDuplicateTextMetadataElementsNuspecQuery;
        public string CursorName => CursorNames.FindCaseInsensitiveDuplicateTextMetadataElementsNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetDuplicateMetadataElements(nuspec, caseSensitive: false, onlyText: true)
                .Any();
        }
    }
}

using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindCaseSensitiveDuplicateTextMetadataElementsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindCaseSensitiveDuplicateTextMetadataElementsNuspecQuery;
        public string CursorName => CursorNames.FindCaseSensitiveDuplicateTextMetadataElementsNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetDuplicateMetadataElements(nuspec, caseSensitive: true, onlyText: true)
                .Any();
        }
    }
}

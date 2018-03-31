using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindCaseSensitiveDuplicateMetadataElementsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindCaseSensitiveDuplicateMetadataElementsNuspecQuery;
        public string CursorName => CursorNames.FindCaseSensitiveDuplicateMetadataElementsNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetDuplicateMetadataElements(nuspec, caseSensitive: true)
                .Any();
        }
    }
}

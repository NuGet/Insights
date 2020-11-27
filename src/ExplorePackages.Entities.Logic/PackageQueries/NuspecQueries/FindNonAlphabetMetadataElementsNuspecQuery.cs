using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindNonAlphabetMetadataElementsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindNonAlphabetMetadataElementsNuspecQuery;
        public string CursorName => CursorNames.FindNonAlphabetMetadataElementsNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetNonAlphabetMetadataElements(nuspec)
                .Any();
        }
    }
}

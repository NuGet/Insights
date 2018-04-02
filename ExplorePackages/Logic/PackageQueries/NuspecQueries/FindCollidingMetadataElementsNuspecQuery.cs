using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindCollidingMetadataElementsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindCollidingMetadataElementsNuspecQuery;
        public string CursorName => CursorNames.FindCollidingMetadataElementsNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetCollidingMetadataElements(nuspec)
                .Any();
        }
    }
}

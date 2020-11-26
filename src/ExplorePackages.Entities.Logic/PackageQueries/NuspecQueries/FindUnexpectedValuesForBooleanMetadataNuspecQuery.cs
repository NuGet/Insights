using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindUnexpectedValuesForBooleanMetadataNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindUnexpectedValuesForBooleanMetadataNuspecQuery;
        public string CursorName => CursorNames.FindUnexpectedValuesForBooleanMetadataNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            return NuspecUtility
                .GetUnexpectedValuesForBooleanMetadata(nuspec)
                .Any();
        }
    }
}

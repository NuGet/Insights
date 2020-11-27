using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindIdsEndingInDotNumberNuspecQuery : INuspecQuery
    {
        private static readonly Regex EndsInDotNumber = new Regex(@"\.\d+$");

        public string Name => PackageQueryNames.FindIdsEndingInDotNumberNuspecQuery;
        public string CursorName => CursorNames.FindIdsEndingInDotNumberNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            var metadataEl = NuspecUtility.GetMetadata(nuspec);
            if (metadataEl == null)
            {
                return false;
            }

            var ns = metadataEl.GetDefaultNamespace();
            var idEl = metadataEl.Element(ns.GetName("id"));
            if (idEl == null)
            {
                return false;
            }

            var id = idEl.Value.TrimEnd();
            return EndsInDotNumber.IsMatch(id);
        }
    }
}

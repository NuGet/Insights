using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindNonAsciiIdsNuspecQuery : INuspecQuery
    {
        private static readonly Regex MatchNonAscii = new Regex(@"[^\u0000-\u007F]", RegexOptions.Compiled);

        public string Name => PackageQueryNames.FindNonAsciiIdsNuspecQuery;
        public string CursorName => CursorNames.FindNonAsciiIdsNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            var id = NuspecUtility.GetOriginalId(nuspec);
            return MatchNonAscii.IsMatch(id);
        }
    }
}

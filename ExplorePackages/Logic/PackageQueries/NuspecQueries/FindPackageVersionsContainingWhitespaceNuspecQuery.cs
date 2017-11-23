using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindPackageVersionsContainingWhitespaceNuspecQuery : INuspecQuery
    {
        private static readonly Regex ContainsWhitespace = new Regex(@"\s", RegexOptions.Compiled);

        public string Name => PackageQueryNames.FindPackageVersionsContainingWhitespaceNuspecQuery;
        public string CursorName => CursorNames.FindPackageVersionsContainingWhitespaceNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            var orignalVersion = NuspecUtility.GetOriginalVersion(nuspec);
            if (orignalVersion == null)
            {
                return false;
            }

            return ContainsWhitespace.IsMatch(orignalVersion);
        }
    }
}

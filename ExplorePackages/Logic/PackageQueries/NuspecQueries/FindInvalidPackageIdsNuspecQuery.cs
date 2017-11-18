using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    /// <summary>
    /// Sources:
    /// <see cref="https://github.com/NuGet/NuGet2/blob/e89c4b2fc6dc2fc752d2fceecd2a2175b0f4ab69/src/Core/Utility/PackageIdValidator.cs"/>
    /// <see cref="https://github.com/NuGet/NuGet.Client/blob/572962f1b1ee890533e5a75163264bd021426dc7/src/NuGet.Core/NuGet.Packaging/PackageCreation/Utility/PackageIdValidator.cs"/>
    /// </summary>
    public class FindInvalidPackageIdsNuspecQuery : INuspecQuery
    {
        private const int MaxPackageIdLength = 100;
        private static readonly Regex IdRegex = new Regex(@"^\w+([_.-]\w+)*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        public string Name => PackageQueryNames.FindInvalidPackageIdsNuspecQuery;
        public string CursorName => CursorNames.FindInvalidPackageIdsNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            var id = NuspecUtility.GetOriginalId(nuspec);

            if (id.Length > MaxPackageIdLength
                || !IdRegex.IsMatch(id))
            {
                return true;
            }

            return false;
        }
    }
}

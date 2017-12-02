using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindInvalidDependencyIdNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindInvalidDependencyIdNuspecQuery;
        public string CursorName => CursorNames.FindInvalidDependencyIdNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            var dependencyEls = NuspecUtility.GetDependencies(nuspec);

            foreach (var dependencyEl in dependencyEls)
            {
                var id = dependencyEl.Attribute("id")?.Value?.Trim();
                if (!StrictPackageIdValidator.IsValid(id))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

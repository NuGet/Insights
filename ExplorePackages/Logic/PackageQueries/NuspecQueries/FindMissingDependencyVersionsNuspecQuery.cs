using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindMissingDependencyVersionsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindMissingDependencyVersionsNuspecQuery;
        public string CursorName => CursorNames.FindMissingDependencyVersionsNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            var dependencyEls = NuspecUtility.GetDependencies(nuspec);

            foreach (var dependencyEl in dependencyEls)
            {
                var version = dependencyEl.Attribute("version");
                if (version == null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

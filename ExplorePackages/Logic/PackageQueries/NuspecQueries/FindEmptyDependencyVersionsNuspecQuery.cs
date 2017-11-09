using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindEmptyDependencyVersionsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindEmptyDependencyVersionsNuspecQuery;
        public string CursorName => CursorNames.FindEmptyDependencyVersionsNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            var dependencyEls = NuspecUtility.GetDependencies(nuspec);

            foreach (var dependencyEl in dependencyEls)
            {
                var version = dependencyEl.Attribute("version");
                if (version != null && version.Value == string.Empty)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

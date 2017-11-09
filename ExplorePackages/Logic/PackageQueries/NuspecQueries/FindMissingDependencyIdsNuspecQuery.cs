using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindMissingDependencyIdsNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindMissingDependencyIdsNuspecQuery;
        public string CursorName => CursorNames.FindMissingDependencyIdsNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            var dependencyEls = NuspecUtility.GetDependencies(nuspec);

            foreach (var dependencyEl in dependencyEls)
            {
                var id = dependencyEl.Attribute("id")?.Value;
                if (string.IsNullOrWhiteSpace(id))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

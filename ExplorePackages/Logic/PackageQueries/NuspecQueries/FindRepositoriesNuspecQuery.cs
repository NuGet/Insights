using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindRepositoriesNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindRepositoriesNuspecQuery;
        public string CursorName => CursorNames.FindRepositoriesNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            var repositoryEl = NuspecUtility.GetRepository(nuspec);

            return repositoryEl != null;
        }
    }
}

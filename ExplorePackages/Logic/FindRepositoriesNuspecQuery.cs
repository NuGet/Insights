using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindRepositoriesNuspecQuery : INuspecQuery
    {
        private readonly ILogger _log;

        public FindRepositoriesNuspecQuery(ILogger log)
        {
            _log = log;
        }

        public string Name => CursorName;
        public string CursorName => CursorNames.FindRepositoriesNuspecQuery;

        public Task<bool> IsMatchAsync(NuspecAndMetadata nuspec)
        {
            return Task.FromResult(HasRepository(nuspec.Document));
        }

        private bool HasRepository(XDocument nuspec)
        {
            if (nuspec == null)
            {
                return false;
            }

            var metadataEl = nuspec
                .Root
                .Elements()
                .Where(x => x.Name.LocalName == "metadata")
                .FirstOrDefault();

            if (metadataEl == null)
            {
                return false;
            }

            var ns = metadataEl.GetDefaultNamespace();
            var repositoryEl = metadataEl.Element(ns.GetName("repository"));
            return repositoryEl != null;
        }
    }
}

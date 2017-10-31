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

        public string Name => NuspecQueryNames.FindRepositoriesNuspecQuery;
        public string CursorName => CursorNames.FindRepositoriesNuspecQuery;

        public Task<bool> IsMatchAsync(NuspecAndMetadata nuspec)
        {
            return Task.FromResult(IsMatch(nuspec.Document));
        }

        private bool IsMatch(XDocument nuspec)
        {
            var metadataEl = NuspecUtility.GetMetadata(nuspec);

            var ns = metadataEl.GetDefaultNamespace();
            var repositoryEl = metadataEl.Element(ns.GetName("repository"));
            return repositoryEl != null;
        }
    }
}

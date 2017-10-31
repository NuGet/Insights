using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindMissingDependencyIdsNuspecQuery : INuspecQuery
    {
        private readonly ILogger _log;

        public FindMissingDependencyIdsNuspecQuery(ILogger log)
        {
            _log = log;
        }

        public string Name => NuspecQueryNames.FindMissingDependencyIdsNuspecQuery;
        public string CursorName => CursorNames.FindMissingDependencyIdsNuspecQuery;

        public Task<bool> IsMatchAsync(NuspecAndMetadata nuspec)
        {
            return Task.FromResult(IsMatch(nuspec.Document));
        }

        private bool IsMatch(XDocument nuspec)
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

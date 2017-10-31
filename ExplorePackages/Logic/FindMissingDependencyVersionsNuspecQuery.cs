using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindMissingDependencyVersionsNuspecQuery : INuspecQuery
    {
        private readonly ILogger _log;

        public FindMissingDependencyVersionsNuspecQuery(ILogger log)
        {
            _log = log;
        }

        public string Name => NuspecQueryNames.FindMissingDependencyVersionsNuspecQuery;
        public string CursorName => CursorNames.FindMissingDependencyVersionsNuspecQuery;

        public Task<bool> IsMatchAsync(NuspecAndMetadata nuspec)
        {
            return Task.FromResult(IsMatch(nuspec.Document));
        }

        private bool IsMatch(XDocument nuspec)
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

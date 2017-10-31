using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindInvalidDependencyVersionsNuspecQuery : INuspecQuery
    {
        private readonly ILogger _log;

        public FindInvalidDependencyVersionsNuspecQuery(ILogger log)
        {
            _log = log;
        }

        public string Name => NuspecQueryNames.FindInvalidDependencyVersionsNuspecQuery;
        public string CursorName => CursorNames.FindInvalidDependencyVersionsNuspecQuery;

        public Task<bool> IsMatchAsync(NuspecAndMetadata nuspec)
        {
            return Task.FromResult(IsMatch(nuspec.Document));
        }

        private bool IsMatch(XDocument nuspec)
        {
            var dependencyEls = NuspecUtility.GetDependencies(nuspec);

            foreach (var dependencyEl in dependencyEls)
            {
                var version = dependencyEl.Attribute("version")?.Value;
                if (!string.IsNullOrEmpty(version)
                    && !VersionRange.TryParse(version, out var parsed))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindEmptyDependencyVersionsNuspecQuery : INuspecQuery
    {
        private readonly ILogger _log;

        public FindEmptyDependencyVersionsNuspecQuery(ILogger log)
        {
            _log = log;
        }

        public string Name => NuspecQueryNames.FindEmptyDependencyVersionsNuspecQuery;
        public string CursorName => CursorNames.FindEmptyDependencyVersionsNuspecQuery;

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
                if (version != null && version.Value == string.Empty)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

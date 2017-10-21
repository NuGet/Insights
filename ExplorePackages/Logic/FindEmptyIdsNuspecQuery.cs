using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindEmptyIdsNuspecQuery : INuspecQuery
    {
        private readonly ILogger _log;

        public FindEmptyIdsNuspecQuery(ILogger log)
        {
            _log = log;
        }

        public string CursorName => CursorNames.FindEmptyIdsNuspecQuery;

        public Task<bool> IsMatchAsync(NuspecAndMetadata nuspec)
        {
            return Task.FromResult(HasEmptyId(nuspec.Document));
        }

        private bool HasEmptyId(XDocument nuspec)
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

            var dependenciesEl = metadataEl.Element(ns.GetName("dependencies"));
            if (dependenciesEl == null)
            {
                return false;
            }

            var dependenyName = ns.GetName("dependency");
            var dependencyEls = dependenciesEl
                .Elements(ns.GetName("group"))
                .SelectMany(x => x.Elements(dependenyName))
                .Concat(dependenciesEl.Elements(dependenyName));

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

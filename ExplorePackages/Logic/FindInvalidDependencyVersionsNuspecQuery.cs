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

        public string Name => CursorName;
        public string CursorName => CursorNames.FindInvalidDependencyVersionsNuspecQuery;

        public Task<bool> IsMatchAsync(NuspecAndMetadata nuspec)
        {
            return Task.FromResult(IsMatch(nuspec.Document));
        }

        private bool IsMatch(XDocument nuspec)
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

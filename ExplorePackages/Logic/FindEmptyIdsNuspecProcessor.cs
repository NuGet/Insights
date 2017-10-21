using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindEmptyIdsNuspecProcessor : INuspecProcessor
    {
        private readonly ILogger _log;

        public FindEmptyIdsNuspecProcessor(ILogger log)
        {
            _log = log;
        }
        
        public Task ProcessAsync(NuspecAndMetadata nuspec)
        {
            if (HasEmptyId(nuspec.Document))
            {
                _log.LogInformation("Empty ID: " + nuspec.Path);
            }

            return Task.CompletedTask;
        }

        private bool HasEmptyId(XDocument nuspec)
        {
            var metadataEl = nuspec
                .Root
                .Elements()
                .Where(x => x.Name.LocalName == "metadata")
                .FirstOrDefault();

            if (metadataEl == null)
            {
                throw new InvalidDataException("No <metadata> element was found!");
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

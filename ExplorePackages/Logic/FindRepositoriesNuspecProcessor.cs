using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindRepositoriesNuspecProcessor : INuspecProcessor
    {
        private readonly ILogger _log;

        public FindRepositoriesNuspecProcessor(ILogger log)
        {
            _log = log;
        }

        public Task ProcessAsync(NuspecAndMetadata nuspec)
        {
            if (HasRepository(nuspec.Document))
            {
                _log.LogInformation("Repository: " + nuspec.Path);
            }

            return Task.CompletedTask;
        }

        private bool HasRepository(XDocument nuspec)
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

            var repositoryEl = metadataEl.Element(ns.GetName("repository"));
            return repositoryEl != null;
        }
    }
}

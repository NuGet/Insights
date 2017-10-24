using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindPackageTypesNuspecQuery : INuspecQuery
    {
        private readonly ILogger _log;

        public FindPackageTypesNuspecQuery(ILogger log)
        {
            _log = log;
        }

        public string Name => CursorName;
        public string CursorName => CursorNames.FindPackageTypesNuspecQuery;

        public Task<bool> IsMatchAsync(NuspecAndMetadata nuspec)
        {
            return Task.FromResult(HasPackageTypes(nuspec.Document));
        }

        private bool HasPackageTypes(XDocument nuspec)
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
            var packageTypesEl = metadataEl.Element(ns.GetName("packageTypes"));
            return packageTypesEl != null;
        }
    }
}

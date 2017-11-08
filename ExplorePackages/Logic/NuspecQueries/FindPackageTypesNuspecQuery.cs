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

        public string Name => NuspecQueryNames.FindPackageTypesNuspecQuery;
        public string CursorName => CursorNames.FindPackageTypesNuspecQuery;

        public Task<bool> IsMatchAsync(NuspecAndMetadata nuspec)
        {
            return Task.FromResult(IsMatch(nuspec.Document));
        }

        private bool IsMatch(XDocument nuspec)
        {
            var metadataEl = NuspecUtility.GetMetadata(nuspec);
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

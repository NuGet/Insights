using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindIdsEndingInDotNumberNuspecQuery : INuspecQuery
    {
        private static readonly Regex EndsInDotNumber = new Regex(@"\.\d+$");
        private readonly ILogger _log;

        public FindIdsEndingInDotNumberNuspecQuery(ILogger log)
        {
            _log = log;
        }

        public string Name => NuspecQueryNames.FindIdsEndingInDotNumberNuspecQuery;
        public string CursorName => CursorNames.FindIdsEndingInDotNumberNuspecQuery;

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
            var idEl = metadataEl.Element(ns.GetName("id"));
            if (idEl == null)
            {
                return false;
            }

            var id = idEl.Value.TrimEnd();
            return EndsInDotNumber.IsMatch(id);
        }
    }
}

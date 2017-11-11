using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class V2Client
    {
        private readonly HttpSource _httpSource;
        private readonly ILogger _log;

        public V2Client(HttpSource httpSource, ILogger log)
        {
            _httpSource = httpSource;
            _log = log;
        }

        public async Task<bool> HasPackageAsync(string baseUrl, string id, string version, bool semVer2)
        {
            var semVerLevel = semVer2 ? "2.0.0" : "1.0.0";
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            var filter = $"Id eq '{id}' and NormalizedVersion eq '{normalizedVersion}' and 1 eq 1";
            var url = $"{baseUrl.TrimEnd('/')}/Packages/$count?$filter={Uri.EscapeDataString(filter)}&semVerLevel={semVerLevel}";
            var count = await _httpSource.ProcessResponseAsync(
                new HttpSourceRequest(url, _log),
                async response =>
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return int.Parse(content);
                },
                _log,
                CancellationToken.None);
            
            if (count == 0)
            {
                return false;
            }
            else if (count == 1)
            {
                return true;
            }

            throw new InvalidDataException("The count returned by V2 should be either 0 or 1.");
        }
    }
}

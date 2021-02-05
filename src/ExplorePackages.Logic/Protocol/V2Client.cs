using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages
{
    public class V2Client
    {
        private const string Projection = "Id,Version,Created,LastEdited,LastUpdated,Published";

        private readonly HttpSource _httpSource;
        private readonly V2Parser _parser;
        private readonly ILogger<V2Client> _logger;

        public V2Client(HttpSource httpSource, V2Parser parser, ILogger<V2Client> logger)
        {
            _httpSource = httpSource;
            _parser = parser;
            _logger = logger;
        }

        public async Task<V2Package> GetPackageOrNullAsync(string baseUrl, string id, string version)
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            var url = $"{baseUrl.TrimEnd('/')}/Packages(Id='{id}',Version='{normalizedVersion}')?hijack=false";
            var page = await ParseV2PageAsync(url);

            if (page.Count == 0)
            {
                return null;
            }
            else if (page.Count == 1)
            {
                return page[0];
            }

            throw new InvalidDataException("The number of packages returned by V2 should be either 0 or 1.");
        }

        private async Task<IReadOnlyList<V2Package>> ParseV2PageAsync(string url)
        {
            var nuGetLogger = _logger.ToNuGetLogger();
            return await _httpSource.ProcessStreamAsync(
                new HttpSourceRequest(url, nuGetLogger),
                stream =>
                {
                    var document = XmlUtility.LoadXml(stream);
                    var page = _parser.ParsePage(document);
                    return Task.FromResult(page);
                },
                nuGetLogger,
                CancellationToken.None);
        }
    }
}

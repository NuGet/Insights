using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
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

        public async Task<IReadOnlyList<V2Package>> GetPackagesAsync(string baseUrl, V2OrderByTimestamp orderBy, DateTimeOffset start, int top)
        {
            string filterField;
            switch (orderBy)
            {
                case V2OrderByTimestamp.Created:
                    filterField = "Created";
                    break;
                case V2OrderByTimestamp.LastEdited:
                    filterField = "LastEdited";
                    break;
                default:
                    throw new NotSupportedException($"The {nameof(orderBy)} value is not supported.");
            }

            var filterValue = $"{filterField} gt DateTime'{start.UtcDateTime:O}'";
            var orderByValue = $"{filterField} asc";

            return await GetPackagesAsync(baseUrl, filterValue, orderByValue, top);
        }

        public async Task<IReadOnlyList<V2Package>> GetPackagesAsync(string baseUrl, string filter, string orderBy, int top)
        {
            filter = filter ?? "1 eq 1";

            var url = $"{baseUrl.TrimEnd('/')}/Packages?$select={Projection}&semVerLevel=2.0.0&$top={top}&$orderby={Uri.EscapeDataString(orderBy)}&$filter={Uri.EscapeDataString(filter)}";

            return await ParseV2PageAsync(url);
        }

        public async Task<V2Package> GetPackageOrNullAsync(string baseUrl, string id, string version, bool semVer2)
        {
            var semVerLevel = semVer2 ? "2.0.0" : "1.0.0";
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            var filter = $"Id eq '{id}' and NormalizedVersion eq '{normalizedVersion}' and 1 eq 1";

            var url = $"{baseUrl.TrimEnd('/')}/Packages?$select={Projection}&$filter={Uri.EscapeDataString(filter)}&semVerLevel={semVerLevel}";

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

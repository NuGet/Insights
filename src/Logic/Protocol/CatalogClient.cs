using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Protocol;

namespace NuGet.Insights
{
    public class CatalogClient
    {
        /*

        NiCatalogLeafItemRecords
        | extend Identity = strcat(LowerId, "/", LowerNormalizedVersion)
        | summarize min(CommitTimestamp), arg_max(CommitTimestamp, Type), leafCount = count() by Identity
        | summarize
            min = min(min_CommitTimestamp),
            minAllPackages = min(CommitTimestamp),
            minAvailablePackages = minif(CommitTimestamp, Type != "PackageDelete")

        min                         | minAllPackages              | minAvailablePackages
        --------------------------- | --------------------------- | ---------------------------
        2015-02-01 06:22:45.8488496 | 2015-10-28 10:22:26.4686283 | 2018-08-08 16:29:16.4488298

        */

        /// <summary>
        /// This is the absolute min in the NuGet.org catalog.
        /// </summary>
        public static readonly DateTimeOffset NuGetOrgMin = DateTimeOffset
            .Parse("2015-02-01T06:22:45.8488496Z")
            .Subtract(TimeSpan.FromTicks(1));

        /// <summary>
        /// This is the point in time that nuget.org started repository signing packages that had already been
        /// published and includes all packages that were repository signed at push time. In other words, we can start
        /// start cursors just before this time and still see all available packages. Using this as a min skips
        /// 2,331,705 catalog leaves (4,251 pages). This is about 35% of the total, as of February, 2021.
        /// </summary>
        public static readonly DateTimeOffset NuGetOrgMinAvailable = DateTimeOffset
            .Parse("2018-08-08T16:29:16.4488298Z")
            .Subtract(TimeSpan.FromTicks(1));

        /// <summary>
        /// This is the point in time that nuget.org package deletion was introduced. In other words, we can start
        /// cursors just before this time and still all deleted and available packages. Using this as a min skips
        /// 630,960 catalog leaves (1,165 pages). This is about 9% of the total, as of February, 2021.
        /// </summary>
        public static readonly DateTimeOffset NuGetOrgMinDeleted = DateTimeOffset
            .Parse("2015-10-28T10:22:26.4686283Z")
            .Subtract(TimeSpan.FromTicks(1));

        private readonly HttpSource _httpSource;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly ILogger<CatalogClient> _logger;

        public CatalogClient(
            HttpSource httpSource,
            ServiceIndexCache serviceIndexCache,
            ILogger<CatalogClient> logger)
        {
            _httpSource = httpSource;
            _serviceIndexCache = serviceIndexCache;
            _logger = logger;
        }

        public async Task<CatalogIndex> GetCatalogIndexAsync(string url)
        {
            return await _httpSource.DeserializeUrlAsync<CatalogIndex>(
                url,
                ignoreNotFounds: false,
                maxTries: 3,
                serializer: CatalogJsonSerialization.Serializer,
                logger: _logger);
        }

        public async Task<DateTimeOffset> GetCommitTimestampAsync()
        {
            var url = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.Catalog);
            var length = 512;
            var nuGetLogger = _logger.ToNuGetLogger();
            var commitTimestamp = await _httpSource.ProcessResponseAsync(
                new HttpSourceRequest(() =>
                {
                    var request = HttpRequestMessageFactory.Create(HttpMethod.Get, url, nuGetLogger);
                    request.Headers.Range = new RangeHeaderValue(0, length);
                    return request;
                }),
                async response =>
                {
                    if (response.StatusCode != HttpStatusCode.PartialContent)
                    {
                        throw new InvalidOperationException($"Expected an HTTP 206 Partial Content response. Got HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
                    }

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var reader = new StreamReader(stream);
                    using var jsonReader = new JsonTextReader(reader);
                    while (jsonReader.Read())
                    {
                        if (jsonReader.TokenType == JsonToken.PropertyName
                            && jsonReader.Depth == 1
                            && (string)jsonReader.Value == "commitTimeStamp")
                        {
                            return jsonReader.ReadAsString();
                        }
                    }

                    throw new InvalidOperationException($"Could not find the commit timestamp in the first {length} bytes of the catalog index.");
                },
                nuGetLogger,
                CancellationToken.None);

            return DateTimeOffset.Parse(commitTimestamp);
        }

        public async Task<CatalogIndex> GetCatalogIndexAsync()
        {
            var url = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.Catalog);
            return await GetCatalogIndexAsync(url);
        }

        public async Task<CatalogPage> GetCatalogPageAsync(string url)
        {
            return await _httpSource.DeserializeUrlAsync<CatalogPage>(
                url,
                ignoreNotFounds: false,
                maxTries: 3,
                serializer: CatalogJsonSerialization.Serializer,
                logger: _logger);
        }

        public async Task<IReadOnlyList<CatalogPageItem>> GetCatalogPageItemsAsync(
            DateTimeOffset minCommitTimestamp,
            DateTimeOffset maxCommitTimestamp)
        {
            var catalogIndex = await GetCatalogIndexAsync();
            return catalogIndex.GetPagesInBounds(minCommitTimestamp, maxCommitTimestamp);
        }

        public async Task<CatalogLeaf> GetCatalogLeafAsync(CatalogLeafItem leaf)
        {
            return await GetCatalogLeafAsync(leaf.Type, leaf.Url);
        }

        public async Task<CatalogLeaf> GetCatalogLeafAsync(CatalogLeafType type, string url)
        {
            switch (type)
            {
                case CatalogLeafType.PackageDelete:
                    return await _httpSource.DeserializeUrlAsync<PackageDeleteCatalogLeaf>(
                        url,
                        ignoreNotFounds: false,
                        maxTries: 3,
                        serializer: CatalogJsonSerialization.Serializer,
                        logger: _logger);
                case CatalogLeafType.PackageDetails:
                    return await _httpSource.DeserializeUrlAsync<PackageDetailsCatalogLeaf>(
                        url,
                        ignoreNotFounds: false,
                        maxTries: 3,
                        serializer: CatalogJsonSerialization.Serializer,
                        logger: _logger);
                default:
                    throw new NotImplementedException($"Catalog leaf type {type} is not supported.");
            }
        }

        public string GetExpectedCatalogLeafRelativePath(string id, string version, DateTimeOffset commitTimestamp)
        {
            return string.Join("/", new[]
            {
                "data",
                commitTimestamp.ToString("yyyy.MM.dd.HH.mm.ss"),
                $"{Uri.EscapeDataString(id.ToLowerInvariant())}.{version.ToLowerInvariant()}.json",
            });
        }

        public async Task<string> GetCatalogBaseUrlAsync()
        {
            var catalogIndexUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.Catalog);
            var lastSlashIndex = catalogIndexUrl.LastIndexOf('/');
            if (lastSlashIndex < 0)
            {
                throw new InvalidOperationException("No slashes were found in the catalog index URL.");
            }

            var catalogBaseUrl = catalogIndexUrl.Substring(0, lastSlashIndex + 1);

            var indexPath = catalogIndexUrl.Substring(catalogBaseUrl.Length);
            if (indexPath != "index.json")
            {
                throw new InvalidOperationException("The catalog index does not have the expected relative path.");
            }

            return catalogBaseUrl;
        }
    }
}

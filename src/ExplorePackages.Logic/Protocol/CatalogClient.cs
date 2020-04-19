using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogClient
    {
        private readonly HttpSource _httpSource;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly IOptionsSnapshot<ExplorePackagesSettings> _options;
        private readonly ILogger<CatalogClient> _logger;

        public CatalogClient(
            HttpSource httpSource,
            ServiceIndexCache serviceIndexCache,
            IOptionsSnapshot<ExplorePackagesSettings> options,
            ILogger<CatalogClient> logger)
        {
            _httpSource = httpSource;
            _serviceIndexCache = serviceIndexCache;
            _options = options;
            _logger = logger;
        }

        public string GetCatalogLeafRelativePath(CatalogLeafEntity leaf)
        {
            if (leaf.RelativePath != null)
            {
                return leaf.RelativePath;
            }

            return GetExpectedCatalogLeafRelativePath(
                leaf.CatalogPackage.Package.Id,
                leaf.CatalogPackage.Package.Version,
                new DateTimeOffset(leaf.CatalogCommit.CommitTimestamp, TimeSpan.Zero));
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

        public async Task<IReadOnlyList<CatalogLeafItem>> GetCatalogLeafItemsAsync(
            IEnumerable<CatalogPageItem> pageItems,
            DateTimeOffset minCommitTimestamp,
            DateTimeOffset maxCommitTimestamp,
            CancellationToken token)
        {
            var leafItemBatches = await TaskProcessor.ExecuteAsync(
                pageItems,
                async pageItem =>
                {
                    var page = await GetCatalogPageAsync(pageItem.Url);
                    return page.GetLeavesInBounds(
                        minCommitTimestamp,
                        maxCommitTimestamp,
                        excludeRedundantLeaves: false);
                },
                workerCount: _options.Value.WorkerCount,
                token: token);

            // Each consumer should ensure values are sorted in an appropriate fashion, but for consistency we
            // sort here as well.
            return leafItemBatches
                .SelectMany(x => x)
                .OrderBy(x => x.CommitTimestamp)
                .ThenBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.ParsePackageVersion())
                .ToList();
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

        public async Task<CatalogLeaf> GetCatalogLeafAsync(CatalogLeafEntity leaf)
        {
            var baseUrl = await GetCatalogBaseUrlAsync();
            var relativePath = GetCatalogLeafRelativePath(leaf);
            var url = baseUrl + relativePath;

            switch (leaf.Type)
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
                    throw new NotImplementedException($"Catalog leaf type {leaf.Type} is not supported.");
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

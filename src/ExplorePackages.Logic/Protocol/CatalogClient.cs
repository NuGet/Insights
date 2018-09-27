using System;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogClient
    {
        private readonly HttpSource _httpSource;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly ILogger<CatalogClient> _logger;

        public CatalogClient(HttpSource httpSource, ServiceIndexCache serviceIndexCache, ILogger<CatalogClient> logger)
        {
            _httpSource = httpSource;
            _serviceIndexCache = serviceIndexCache;
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

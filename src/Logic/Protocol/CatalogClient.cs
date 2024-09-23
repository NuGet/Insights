// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

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
        /// This is the earliest commit timestamp in the NuGet.org catalog.
        /// </summary>
        public static readonly DateTimeOffset NuGetOrgFirstCommit = DateTimeOffset
            .Parse("2015-02-01T06:22:45.8488496Z", CultureInfo.InvariantCulture);

        /// <summary>
        /// This is a min value right before (by 1 tick) the earliest commit timestamp in the NuGet.org catalog.
        /// </summary>
        public static readonly DateTimeOffset NuGetOrgMin = NuGetOrgFirstCommit
            .Subtract(TimeSpan.FromTicks(1));

        /// <summary>
        /// This is the point in time that nuget.org started repository signing packages that had already been
        /// published and includes all packages that were repository signed at push time. In other words, we can start
        /// start cursors just before this time and still see all available packages. Using this as a min skips
        /// 2,331,705 catalog leaves (4,251 pages). This is about 35% of the total, as of February, 2021.
        ///
        /// Generally this should not be used because it entirely skips some deleted packages and the data files
        /// include empty, marker records for deleted packages. If the data files are built using this min timestamp
        /// then these records will be missing and will fail to import into Kusto during the data consistency
        /// (validation) step with the catalog leaf item table.
        /// </summary>
        public static readonly DateTimeOffset NuGetOrgMinAvailable = DateTimeOffset
            .Parse("2018-08-08T16:29:16.4488298Z", CultureInfo.InvariantCulture)
            .Subtract(TimeSpan.FromTicks(1));

        /// <summary>
        /// This is the point in time that nuget.org package deletion was introduced. In other words, we can start
        /// cursors just before this time and still all deleted and available packages. Using this as a min skips
        /// 630,960 catalog leaves (1,165 pages). This is about 9% of the total, as of February, 2021.
        /// </summary>
        public static readonly DateTimeOffset NuGetOrgMinDeleted = DateTimeOffset
            .Parse("2015-10-28T10:22:26.4686283Z", CultureInfo.InvariantCulture)
            .Subtract(TimeSpan.FromTicks(1));

        private readonly Func<HttpClient> _httpClientFactory;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly ILogger<CatalogClient> _logger;

        public CatalogClient(
            Func<HttpClient> httpClientFactory,
            ServiceIndexCache serviceIndexCache,
            ILogger<CatalogClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _serviceIndexCache = serviceIndexCache;
            _logger = logger;
        }

        public async Task<CatalogIndex> GetCatalogIndexAsync()
        {
            var url = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.Catalog);
            return await GetCatalogIndexAsync(url);
        }

        private async Task<CatalogIndex> GetCatalogIndexAsync(string url)
        {
            var httpClient = _httpClientFactory();
            return await httpClient.DeserializeUrlAsync<CatalogIndex>(url, _logger);
        }

        public async Task<CatalogPage> GetCatalogPageAsync(string url)
        {
            var httpClient = _httpClientFactory();
            return await httpClient.DeserializeUrlAsync<CatalogPage>(url, _logger);
        }

        public async Task<CatalogLeaf> GetCatalogLeafAsync(CatalogLeafType type, string url)
        {
            var httpClient = _httpClientFactory();
            switch (type)
            {
                case CatalogLeafType.PackageDelete:
                    return await httpClient.DeserializeUrlAsync<PackageDeleteCatalogLeaf>(url, _logger);
                case CatalogLeafType.PackageDetails:
                    return await httpClient.DeserializeUrlAsync<PackageDetailsCatalogLeaf>(url, _logger);
                default:
                    throw new NotImplementedException($"Catalog leaf type {type} is not supported.");
            }
        }
    }
}

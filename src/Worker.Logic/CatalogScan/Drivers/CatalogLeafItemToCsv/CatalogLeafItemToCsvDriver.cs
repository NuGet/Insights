// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker.CatalogLeafItemToCsv
{
    public class CatalogLeafItemToCsvDriver : ICatalogLeafScanNonBatchDriver, ICsvResultStorage<CatalogLeafItemRecord>
    {
        private readonly CsvTemporaryStorageFactory _tempStorageFactory;
        private readonly CatalogClient _catalogClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ICsvTemporaryStorage _tempStorage;

        public CatalogLeafItemToCsvDriver(
            CsvTemporaryStorageFactory storageFactory,
            CatalogClient catalogClient,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _tempStorageFactory = storageFactory;
            _catalogClient = catalogClient;
            _options = options;
            _tempStorage = storageFactory.Create(this).Single();
        }

        public string ResultContainerName => _options.Value.CatalogLeafItemContainerName;

        public List<CatalogLeafItemRecord> Prune(List<CatalogLeafItemRecord> records, bool isFinalPrune)
        {
            return records
                .Distinct()
                .OrderBy(x => x.CommitTimestamp)
                .ThenBy(x => x.Identity, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await _tempStorageFactory.InitializeAsync(indexScan.StorageSuffix);
            await _tempStorage.InitializeAsync(indexScan.StorageSuffix);
        }

        public Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            return Task.FromResult(CatalogIndexScanResult.ExpandAllLeaves);
        }

        public async Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            var page = await _catalogClient.GetCatalogPageAsync(pageScan.Url);
            var items = page.GetLeavesInBounds(pageScan.Min, pageScan.Max, excludeRedundantLeaves: false);
            var records = items
                .Select(x => new CatalogLeafItemRecord
                {
                    CommitId = x.CommitId,
                    CommitTimestamp = x.CommitTimestamp,
                    LowerId = x.PackageId.ToLowerInvariant(),
                    Identity = $"{x.PackageId}/{x.ParsePackageVersion().ToNormalizedString()}".ToLowerInvariant(),
                    Id = x.PackageId,
                    Version = x.PackageVersion,
                    Type = x.Type,
                    Url = x.Url,

                    PageUrl = pageScan.Url,
                })
                .ToList();

            await _tempStorage.AppendAsync(
                pageScan.StorageSuffix,
                new[] { new CsvRecordSet<CatalogLeafItemRecord>(pageScan.Url, records) });

            return CatalogPageScanResult.Processed;
        }

        public Task<DriverResult> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            throw new NotImplementedException();
        }

        public async Task StartAggregateAsync(CatalogIndexScan indexScan)
        {
            await _tempStorage.StartAggregateAsync(indexScan.GetScanId(), indexScan.StorageSuffix);
        }

        public async Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan)
        {
            return await _tempStorage.IsAggregateCompleteAsync(indexScan.GetScanId(), indexScan.StorageSuffix);
        }

        public async Task FinalizeAsync(CatalogIndexScan indexScan)
        {
            await _tempStorage.FinalizeAsync(indexScan.StorageSuffix);
            await _tempStorageFactory.FinalizeAsync(indexScan.StorageSuffix);
        }

        public async Task StartCustomExpandAsync(CatalogIndexScan indexScan)
        {
            await _tempStorage.StartCustomExpandAsync(indexScan);
        }

        public async Task<bool> IsCustomExpandCompleteAsync(CatalogIndexScan indexScan)
        {
            return await _tempStorage.IsCustomExpandCompleteAsync(indexScan);
        }

        public Task<ICatalogLeafItem> MakeReprocessItemOrNullAsync(CatalogLeafItemRecord record)
        {
            throw new NotImplementedException();
        }
    }
}

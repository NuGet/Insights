// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker
{
    public class CatalogScanStorageService
    {
        private static readonly IReadOnlyDictionary<string, CatalogLeafScan> EmptyLeafIdToLeafScans = new Dictionary<string, CatalogLeafScan>();

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<CatalogScanStorageService> _logger;

        public CatalogScanStorageService(
            ServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<CatalogScanStorageService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await (await GetIndexScanTableAsync()).CreateIfNotExistsAsync(retry: true);
        }

        public async Task InitializePageScanTableAsync(string storageSuffix)
        {
            await (await GetPageScanTableAsync(storageSuffix)).CreateIfNotExistsAsync(retry: true);
        }

        public async Task InitializeLeafScanTableAsync(string storageSuffix)
        {
            await (await GetLeafScanTableAsync(storageSuffix)).CreateIfNotExistsAsync(retry: true);
        }

        public async Task DeleteChildTablesAsync(string storageSuffix)
        {
            await (await GetLeafScanTableAsync(storageSuffix)).DeleteAsync();
            await (await GetPageScanTableAsync(storageSuffix)).DeleteAsync();
        }

        public string GenerateFindLatestScanId(CatalogIndexScan scan)
        {
            return scan.ScanId + "-fl";
        }

        public string GenerateFindLatestStorageSuffix(CatalogIndexScan scan)
        {
            return scan.StorageSuffix + "fl";
        }

        public async Task InsertAsync(CatalogIndexScan indexScan)
        {
            var table = await GetIndexScanTableAsync();
            try
            {
                var response = await table.AddEntityAsync(indexScan);
                indexScan.UpdateETag(response);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict && ex.ErrorCode == TableErrorCode.EntityAlreadyExists)
            {
                _logger.LogTransientWarning("Catalog index scan {DriverType} {ScanId} was already inserted.", indexScan.DriverType, indexScan.ScanId);
            }
        }

        public async Task<IReadOnlyList<CatalogPageScan>> GetPageScansAsync(string storageSuffix, string scanId)
        {
            var table = await GetPageScanTableAsync(storageSuffix);
            return await table
                .QueryAsync<CatalogPageScan>(x => x.PartitionKey == scanId)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<IReadOnlyList<CatalogLeafScan>> GetLeafScansAsync(string storageSuffix, string scanId, string pageId)
        {
            var table = await GetLeafScanTableAsync(storageSuffix);
            return await GetLeafScansAsync(table, scanId, pageId);
        }

        private async Task<IReadOnlyList<CatalogLeafScan>> GetLeafScansAsync(TableClientWithRetryContext table, string scanId, string pageId)
        {
            return await table
                .QueryAsync<CatalogLeafScan>(x => x.PartitionKey == CatalogLeafScan.GetPartitionKey(scanId, pageId))
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<IReadOnlyDictionary<string, CatalogLeafScan>> GetLeafScansAsync(string storageSuffix, string scanId, string pageId, IEnumerable<string> leafIds)
        {
            var sortedLeafIds = leafIds.OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (sortedLeafIds.Count == 0)
            {
                return EmptyLeafIdToLeafScans;
            }
            else if (sortedLeafIds.Count == 1)
            {
                var leafScan = await GetLeafScanAsync(storageSuffix, scanId, pageId, sortedLeafIds[0]);
                if (leafScan == null)
                {
                    return EmptyLeafIdToLeafScans;
                }
                else
                {
                    return new Dictionary<string, CatalogLeafScan> { { leafScan.LeafId, leafScan } };
                }
            }

            var min = sortedLeafIds[0];
            var max = sortedLeafIds[sortedLeafIds.Count - 1];

            var table = await GetLeafScanTableAsync(storageSuffix);
            var leafScans = await table
                .QueryAsync<CatalogLeafScan>(x =>
                    x.PartitionKey == CatalogLeafScan.GetPartitionKey(scanId, pageId)
                    && x.RowKey.CompareTo(min) >= 0
                    && x.RowKey.CompareTo(max) <= 0)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());
            var uniqueLeafIds = sortedLeafIds.ToHashSet();
            return leafScans
                .Where(x => uniqueLeafIds.Contains(x.LeafId))
                .ToDictionary(x => x.LeafId);
        }

        public async Task InsertAsync(IReadOnlyList<CatalogPageScan> pageScans)
        {
            foreach (var group in pageScans.GroupBy(x => x.StorageSuffix))
            {
                var table = await GetPageScanTableAsync(group.Key);
                await SubmitBatchesAsync(group.Key, table, group, (b, i) => b.AddEntity(i));
            }
        }

        public async Task InsertMissingAsync(IReadOnlyList<CatalogLeafScan> leafScans, bool allowExtra)
        {
            foreach (var group in leafScans.GroupBy(x => new { x.StorageSuffix, x.ScanId, x.PageId }))
            {
                var table = await GetLeafScanTableAsync(group.Key.StorageSuffix);
                var createdLeaves = await GetLeafScansAsync(table, group.Key.ScanId, group.Key.PageId);

                var allUrls = group.Select(x => x.Url).ToHashSet();
                var createdUrls = createdLeaves.Select(x => x.Url).ToHashSet();
                var uncreatedUrls = allUrls.Except(createdUrls).ToHashSet();

                if (!allowExtra)
                {
                    var extraUrls = createdUrls.Except(allUrls).Order(StringComparer.Ordinal).ToList();

                    if (extraUrls.Count > 0)
                    {
                        _logger.LogError(
                            "{Count} extra leaf scan entities were found. Sample: {ExtraUrls}",
                            extraUrls.Count,
                            extraUrls.Take(5));
                        throw new InvalidOperationException("There should not be any extra leaf scan entities.");
                    }
                }

                var uncreatedLeafScans = group
                    .Where(x => uncreatedUrls.Contains(x.Url))
                    .ToList();

                await SubmitBatchesAsync(group.Key.StorageSuffix, table, uncreatedLeafScans, (b, i) => b.AddEntity(i));

                foreach (var scan in uncreatedLeafScans)
                {
                    if (scan.Max - scan.Min <= _options.Value.LeafLevelTelemetryThreshold)
                    {
                        _telemetryClient.TrackMetric($"{nameof(CatalogScanStorageService)}.{nameof(InsertMissingAsync)}.{nameof(CatalogLeafScan)}", 1, new Dictionary<string, string>
                        {
                            { nameof(CatalogLeafScanMessage.StorageSuffix), scan.StorageSuffix },
                            { nameof(CatalogLeafScanMessage.ScanId), scan.ScanId },
                            { nameof(CatalogLeafScanMessage.PageId), scan.PageId },
                            { nameof(CatalogLeafScanMessage.LeafId), scan.LeafId },
                        });
                    }
                }
            }
        }

        private async Task SubmitBatchesAsync<T>(
            string storageSuffix,
            TableClientWithRetryContext table,
            IEnumerable<T> entities,
            Action<MutableTableTransactionalBatch, T> doOperation) where T : class, ITableEntity, new()
        {
            T firstEntity = null;
            try
            {
                var batch = new MutableTableTransactionalBatch(table);
                foreach (var entity in entities)
                {
                    if (batch.Count >= StorageUtility.MaxBatchSize)
                    {
                        await batch.SubmitBatchAsync();
                        batch = new MutableTableTransactionalBatch(table);
                    }

                    doOperation(batch, entity);
                    if (batch.Count == 1)
                    {
                        firstEntity = entity;
                    }
                }

                await batch.SubmitBatchIfNotEmptyAsync();
            }
            catch (RequestFailedException ex) when (ex.Status > 0)
            {
                _logger.LogTransientWarning(
                    ex,
                    "Batch failed due to HTTP {Status}, with storage suffix '{StorageSuffix}', first partition key '{PartitionKey}', first row key '{RowKey}'.",
                    ex.Status,
                    storageSuffix,
                    firstEntity.PartitionKey,
                    firstEntity.RowKey);
                throw;
            }
        }

        public async Task<IReadOnlyList<CatalogIndexScan>> GetIndexScansAsync()
        {
            return await (await GetIndexScanTableAsync())
                .QueryAsync<CatalogIndexScan>()
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<Dictionary<CatalogScanDriverType, List<CatalogIndexScan>>> GetAllLatestIndexScansAsync(int maxEntities)
        {
            var pks = CatalogScanDriverMetadata.StartableDriverTypes.Select(x => x.ToString()).ToList();
            var minPk = pks.Min(StringComparer.Ordinal);
            var maxPk = pks.Max(StringComparer.Ordinal);

            var table = await GetIndexScanTableAsync();
            var query = table.QueryAsync<CatalogIndexScan>(x => x.PartitionKey.CompareTo(minPk) >= 0 && x.PartitionKey.CompareTo(maxPk) <= 0);

            var output = CatalogScanDriverMetadata.StartableDriverTypes.ToDictionary(x => x, x => new List<CatalogIndexScan>());
            var completed = 0;

            await foreach (var item in query)
            {
                if (output.TryGetValue(item.DriverType, out var list) && list.Count < maxEntities)
                {
                    list.Add(item);
                    if (list.Count == maxEntities)
                    {
                        completed++;
                        if (completed == output.Count)
                        {
                            break;
                        }
                    }
                }
            }

            return output;
        }

        public async Task<IReadOnlyList<CatalogIndexScan>> GetLatestIndexScansAsync(CatalogScanDriverType driverType, int maxEntities)
        {
            return await (await GetIndexScanTableAsync())
                .QueryAsync<CatalogIndexScan>(x => x.PartitionKey == driverType.ToString())
                .Take(maxEntities)
                .ToListAsync();
        }

        public async Task DeleteOldIndexScansAsync(CatalogScanDriverType driverType, string currentScanId)
        {
            var table = await GetIndexScanTableAsync();

            var oldScans = await table
                .QueryAsync<CatalogIndexScan>(x => x.PartitionKey == driverType.ToString()
                                                && x.RowKey.CompareTo(currentScanId) > 0)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());

            var oldScansToDelete = oldScans
                .OrderByDescending(x => x.Created)
                .Skip(_options.Value.OldCatalogIndexScansToKeep)
                .OrderBy(x => x.Created)
                .Where(x => x.State.IsTerminal())
                .ToList();

            _logger.LogInformation(
                "Deleting {DeleteCount} old catalog index scans ({DriverType} scans older than scan {ScanId}, keeping {KeepCount} for history).",
                oldScansToDelete.Count,
                driverType,
                currentScanId,
                oldScans.Count - oldScansToDelete.Count);

            foreach (var scan in oldScansToDelete)
            {
                try
                {
                    await DeleteAsync(scan);
                }
                catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
                {
                    _logger.LogTransientWarning("The old catalog index scan {ScanId} was already deleted.", scan.ScanId);
                }
            }
        }

        public async Task<CatalogIndexScan> GetIndexScanAsync(CatalogScanDriverType driverType, string scanId)
        {
            return await (await GetIndexScanTableAsync())
                .GetEntityOrNullAsync<CatalogIndexScan>(driverType.ToString(), scanId);
        }

        public async Task<CatalogPageScan> GetPageScanAsync(string storageSuffix, string scanId, string pageId)
        {
            return await (await GetPageScanTableAsync(storageSuffix))
                .GetEntityOrNullAsync<CatalogPageScan>(scanId, pageId);
        }

        public async Task<CatalogLeafScan> GetLeafScanAsync(string storageSuffix, string scanId, string pageId, string leafId)
        {
            return await (await GetLeafScanTableAsync(storageSuffix))
                .GetEntityOrNullAsync<CatalogLeafScan>(CatalogLeafScan.GetPartitionKey(scanId, pageId), leafId);
        }

        public async Task ReplaceAsync(CatalogIndexScan scan)
        {
            var runtime = scan.Started.HasValue ? (scan.Completed ?? DateTimeOffset.UtcNow) - scan.Started.Value : TimeSpan.Zero;

            _telemetryClient.TrackMetric(
                "CatalogIndexScan.RuntimeMinutes",
                runtime.TotalMinutes,
                new Dictionary<string, string>
                {
                    { nameof(scan.BucketRanges), scan.BucketRanges },
                    { nameof(scan.Completed), scan.Completed?.ToString("O") },
                    { nameof(scan.ContinueUpdate), scan.ContinueUpdate ? "true" : "false" },
                    { nameof(scan.Created), scan.Created.ToString("O") },
                    { nameof(scan.CursorName), scan.CursorName },
                    { nameof(scan.DriverType), scan.DriverType.ToString() },
                    { nameof(scan.Max), scan.Max.ToString("O") },
                    { nameof(scan.Min), scan.Min.ToString("O") },
                    { nameof(scan.OnlyLatestLeaves), scan.OnlyLatestLeaves ? "true" : "false" },
                    { nameof(scan.ParentDriverType), scan.ParentDriverType?.ToString() },
                    { nameof(scan.ParentScanId), scan.ParentScanId },
                    { nameof(scan.Result), scan.Result?.ToString() },
                    { nameof(scan.ScanId), scan.ScanId },
                    { nameof(scan.Started), scan.Started?.ToString("O") },
                    { nameof(scan.State), scan.State.ToString() },
                    { nameof(scan.StorageSuffix), scan.StorageSuffix },
                });

            _logger.LogInformation("Replacing catalog index scan {ScanId}, state: {State}.", scan.ScanId, scan.State);
            var response = await (await GetIndexScanTableAsync()).UpdateEntityAsync(scan, scan.ETag, TableUpdateMode.Replace);
            scan.UpdateETag(response);
        }

        public async Task ReplaceAsync(CatalogPageScan pageScan)
        {
            _logger.LogInformation("Replacing catalog page scan {ScanId}, page {PageId}, state: {State}.", pageScan.ScanId, pageScan.PageId, pageScan.State);
            var response = await (await GetPageScanTableAsync(pageScan.StorageSuffix)).UpdateEntityAsync(pageScan, pageScan.ETag, TableUpdateMode.Replace);
            pageScan.UpdateETag(response);
        }

        public async Task ReplaceAsync(CatalogLeafScan leafScan)
        {
            var response = await (await GetLeafScanTableAsync(leafScan.StorageSuffix)).UpdateEntityAsync(leafScan, leafScan.ETag, TableUpdateMode.Replace);
            leafScan.UpdateETag(response);
        }

        public async Task ReplaceAsync(IEnumerable<CatalogLeafScan> leafScans)
        {
            await SubmitLeafBatchesAsync(leafScans, (b, i) => b.UpdateEntity(i, i.ETag, TableUpdateMode.Replace));
        }

        public async Task DeleteAsync(IEnumerable<CatalogLeafScan> leafScans)
        {
            var leafScansList = leafScans.ToList();
            if (leafScansList.Count == 1)
            {
                await DeleteAsync(leafScansList[0]);
                return;
            }

            try
            {
                await SubmitLeafBatchesAsync(leafScansList, (b, i) => b.DeleteEntity(i.PartitionKey, i.RowKey, i.ETag));

                foreach (var leafScan in leafScansList)
                {
                    if (leafScan.Max - leafScan.Min <= _options.Value.LeafLevelTelemetryThreshold)
                    {
                        _telemetryClient.TrackMetric($"{nameof(CatalogScanStorageService)}.{nameof(DeleteAsync)}.Batch.{nameof(CatalogLeafScan)}", 1, new Dictionary<string, string>
                        {
                            { nameof(CatalogLeafScanMessage.StorageSuffix), leafScan.StorageSuffix },
                            { nameof(CatalogLeafScanMessage.ScanId), leafScan.ScanId },
                            { nameof(CatalogLeafScanMessage.PageId), leafScan.PageId },
                            { nameof(CatalogLeafScanMessage.LeafId), leafScan.LeafId },
                        });
                    }
                }
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                // Try individually, to ensure each entity is deleted if it exists.
                foreach (var scan in leafScansList)
                {
                    await DeleteAsync(scan);
                }
            }
        }

        private async Task SubmitLeafBatchesAsync(IEnumerable<CatalogLeafScan> leafScans, Action<MutableTableTransactionalBatch, CatalogLeafScan> doOperation)
        {
            if (!leafScans.Any())
            {
                return;
            }

            var storageSuffixAndPartitionKeys = leafScans.Select(x => new { x.StorageSuffix, x.PartitionKey }).Distinct();
            if (storageSuffixAndPartitionKeys.Count() > 1)
            {
                throw new ArgumentException("All leaf scans must have the same storage suffix and partition key.");
            }

            var storageSuffix = storageSuffixAndPartitionKeys.Single().StorageSuffix;
            var table = await GetLeafScanTableAsync(storageSuffix);
            await SubmitBatchesAsync(storageSuffix, table, leafScans, doOperation);
        }

        public async Task<int> GetPageScanCountLowerBoundAsync(string storageSuffix, string scanId)
        {
            var table = await GetPageScanTableAsync(storageSuffix);
            return await table.GetEntityCountLowerBoundAsync(scanId, _telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<int> GetLeafScanCountLowerBoundAsync(string storageSuffix, string scanId)
        {
            var table = await GetLeafScanTableAsync(storageSuffix);
            return await table.GetEntityCountLowerBoundAsync(
                CatalogLeafScan.GetPartitionKey(scanId, string.Empty),
                CatalogLeafScan.GetPartitionKey(scanId, char.MaxValue.ToString()),
                _telemetryClient.StartQueryLoopMetrics());
        }

        public async Task DeleteAsync(CatalogIndexScan indexScan)
        {
            _logger.LogInformation("Deleting {DriverType} catalog scan with ID {ScanId}.", indexScan.DriverType, indexScan.ScanId);
            await (await GetIndexScanTableAsync()).DeleteEntityAsync(indexScan, indexScan.ETag);
        }

        public async Task DeleteAsync(CatalogPageScan pageScan)
        {
            await (await GetPageScanTableAsync(pageScan.StorageSuffix)).DeleteEntityAsync(pageScan, pageScan.ETag);
        }

        public async Task DeleteAsync(CatalogLeafScan leafScan)
        {
            try
            {
                await (await GetLeafScanTableAsync(leafScan.StorageSuffix)).DeleteEntityAsync(leafScan, leafScan.ETag);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                _logger.LogTransientWarning(
                    ex,
                    "Catalog leaf scan with storage suffix {StorageSuffix}, partition key {PartitionKey}, and row key {RowKey} was already deleted.",
                    leafScan.StorageSuffix,
                    leafScan.PartitionKey,
                    leafScan.RowKey);
            }

            if (leafScan.Max - leafScan.Min <= _options.Value.LeafLevelTelemetryThreshold)
            {
                _telemetryClient.TrackMetric($"{nameof(CatalogScanStorageService)}.{nameof(DeleteAsync)}.Single.{nameof(CatalogLeafScan)}", 1, new Dictionary<string, string>
                {
                    { nameof(CatalogLeafScanMessage.StorageSuffix), leafScan.StorageSuffix },
                    { nameof(CatalogLeafScanMessage.ScanId), leafScan.ScanId },
                    { nameof(CatalogLeafScanMessage.PageId), leafScan.PageId },
                    { nameof(CatalogLeafScanMessage.LeafId), leafScan.LeafId },
                });
            }
        }

        private async Task<TableClientWithRetryContext> GetIndexScanTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.CatalogIndexScanTableName);
        }

        private async Task<TableClientWithRetryContext> GetPageScanTableAsync(string suffix)
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient($"{_options.Value.CatalogPageScanTableName}{suffix}");
        }

        public string GetLeafScanTableName(string suffix)
        {
            return $"{_options.Value.CatalogLeafScanTableName}{suffix}";
        }

        public async Task<TableClientWithRetryContext> GetLeafScanTableAsync(string suffix)
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(GetLeafScanTableName(suffix));
        }
    }
}

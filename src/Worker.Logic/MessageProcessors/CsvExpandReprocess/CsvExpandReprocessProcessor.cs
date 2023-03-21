// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace NuGet.Insights.Worker
{
    public class CsvExpandReprocessProcessor<T> : IMessageProcessor<CsvExpandReprocessMessage<T>> where T : ICsvRecord
    {
        private readonly AppendResultStorageService _appendResultStorageService;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly ICsvResultStorage<T> _csvStorage;
        private readonly ILogger<CsvExpandReprocessProcessor<T>> _logger;

        public CsvExpandReprocessProcessor(
            AppendResultStorageService appendResultStorageService,
            TaskStateStorageService taskStateStorageService,
            CatalogScanStorageService catalogScanStorageService,
            ICsvResultStorage<T> csvStorage,
            ILogger<CsvExpandReprocessProcessor<T>> logger)
        {
            _appendResultStorageService = appendResultStorageService;
            _taskStateStorageService = taskStateStorageService;
            _catalogScanStorageService = catalogScanStorageService;
            _csvStorage = csvStorage;
            _logger = logger;
        }

        public async Task ProcessAsync(CsvExpandReprocessMessage<T> message, long dequeueCount)
        {
            var taskState = await _taskStateStorageService.GetAsync(message.TaskStateKey);
            if (taskState == null)
            {
                _logger.LogWarning("No matching task state was found.");
                return;
            }

            var indexScan = await _catalogScanStorageService.GetIndexScanAsync(message.CursorName, message.ScanId);
            if (indexScan == null)
            {
                _logger.LogWarning("No matching index scan was found.");
                return;
            }

            var records = await _appendResultStorageService.ReadAsync<T>(_csvStorage.ResultContainerName, message.Bucket);

            var items = new List<(ICatalogLeafItem LeafItem, string PageUrl)>();
            foreach (var record in records)
            {
                (var item, var pageUrl) = await _csvStorage.MakeReprocessItemOrNullAsync(record);
                if (item != null)
                {
                    items.Add((item, pageUrl));
                }
            }

            var reprocessLeaves = items
                .Select(x => new CatalogLeafScan(indexScan.StorageSuffix, indexScan.GetScanId(), GetPageId(x.LeafItem.PackageId), GetLeafId(x.LeafItem.PackageVersion))
                {
                    DriverType = indexScan.DriverType,
                    DriverParameters = indexScan.DriverParameters,
                    Url = x.LeafItem.Url,
                    PageUrl = x.PageUrl,
                    LeafType = x.LeafItem.Type,
                    CommitId = x.LeafItem.CommitId,
                    CommitTimestamp = x.LeafItem.CommitTimestamp,
                    PackageId = x.LeafItem.PackageId,
                    PackageVersion = x.LeafItem.PackageVersion,
                })
                .GroupBy(x => x.Url)
                .Select(g => g.OrderByDescending(x => x.CommitTimestamp).First())
                .ToList();

            await _catalogScanStorageService.InsertMissingAsync(reprocessLeaves);

            await _taskStateStorageService.DeleteAsync(taskState);
        }

        private static string GetPageId(string packageId)
        {
            return packageId.ToLowerInvariant();
        }

        private static string GetLeafId(string packageVersion)
        {
            return NuGetVersion.Parse(packageVersion).ToNormalizedString().ToLowerInvariant();
        }
    }
}

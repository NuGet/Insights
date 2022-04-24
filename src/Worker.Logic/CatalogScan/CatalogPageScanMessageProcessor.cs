// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights.Worker
{
    public class CatalogPageScanMessageProcessor : IMessageProcessor<CatalogPageScanMessage>
    {
        private readonly CatalogClient _catalogClient;
        private readonly ICatalogScanDriverFactory _driverFactory;
        private readonly CatalogScanStorageService _storageService;
        private readonly CatalogScanExpandService _expandService;
        private readonly ILogger<CatalogPageScanMessageProcessor> _logger;

        public CatalogPageScanMessageProcessor(
            CatalogClient catalogClient,
            ICatalogScanDriverFactory driverFactory,
            CatalogScanStorageService storageService,
            CatalogScanExpandService expandService,
            ILogger<CatalogPageScanMessageProcessor> logger)
        {
            _catalogClient = catalogClient;
            _driverFactory = driverFactory;
            _storageService = storageService;
            _expandService = expandService;
            _logger = logger;
        }

        public async Task ProcessAsync(CatalogPageScanMessage message, long dequeueCount)
        {
            var scan = await _storageService.GetPageScanAsync(message.StorageSuffix, message.ScanId, message.PageId);
            if (scan == null)
            {
                _logger.LogWarning("No matching page scan was found.");
                return;
            }

            var driver = _driverFactory.Create(scan.DriverType);

            var result = await driver.ProcessPageAsync(scan);

            switch (result)
            {
                case CatalogPageScanResult.Processed:
                    await _storageService.DeleteAsync(scan);
                    break;
                case CatalogPageScanResult.ExpandAllowDuplicates:
                    await ExpandAsync(scan, excludeRedundantLeaves: false);
                    break;
                case CatalogPageScanResult.ExpandRemoveDuplicates:
                    await ExpandAsync(scan, excludeRedundantLeaves: true);
                    break;
                default:
                    throw new NotSupportedException($"Catalog page scan result '{result}' is not supported.");
            }
        }

        private async Task ExpandAsync(CatalogPageScan scan, bool excludeRedundantLeaves)
        {
            var lazyLeafScansTask = new Lazy<Task<List<CatalogLeafScan>>>(() => InitializeLeavesAsync(scan, excludeRedundantLeaves));

            // Created: no-op
            if (scan.State == CatalogPageScanState.Created)
            {
                scan.State = CatalogPageScanState.Expanding;
                await _storageService.ReplaceAsync(scan);
            }

            // Expanding: create a record for each leaf
            if (scan.State == CatalogPageScanState.Expanding)
            {
                var leafScans = await lazyLeafScansTask.Value;
                await InsertLeafScansAsync(scan.StorageSuffix, scan.GetScanId(), scan.GetPageId(), leafScans);

                scan.State = CatalogPageScanState.Enqueuing;
                await _storageService.ReplaceAsync(scan);
            }

            // Enqueueing: enqueue a message for each leaf
            if (scan.State == CatalogPageScanState.Enqueuing)
            {
                var leafScans = await lazyLeafScansTask.Value;
                await _expandService.EnqueueLeafScansAsync(leafScans);

                scan.State = CatalogPageScanState.Complete;
                await _storageService.ReplaceAsync(scan);
            }

            // Complete -> Deleted
            if (scan.State == CatalogPageScanState.Complete)
            {
                await _storageService.DeleteAsync(scan);
            }
        }

        private async Task InsertLeafScansAsync(string storageSuffix, string scanId, string pageId, IReadOnlyList<CatalogLeafScan> leafScans)
        {
            var createdLeaves = await _storageService.GetLeafScansAsync(storageSuffix, scanId, pageId);

            var allUrls = leafScans.Select(x => x.Url).ToHashSet();
            var createdUrls = createdLeaves.Select(x => x.Url).ToHashSet();
            var uncreatedUrls = allUrls.Except(createdUrls).ToHashSet();

            if (createdUrls.Except(allUrls).Any())
            {
                throw new InvalidOperationException("There should not be any extra leaf scan entities.");
            }

            var uncreatedLeafScans = leafScans
                .Where(x => uncreatedUrls.Contains(x.Url))
                .ToList();
            await _storageService.InsertAsync(uncreatedLeafScans);
        }

        private async Task<List<CatalogLeafScan>> InitializeLeavesAsync(CatalogPageScan scan, bool excludeRedundantLeaves)
        {
            _logger.LogInformation("Loading catalog page URL: {Url}", scan.Url);
            var page = await _catalogClient.GetCatalogPageAsync(scan.Url);

            var items = page.GetLeavesInBounds(scan.Min, scan.Max, excludeRedundantLeaves);
            var leafItemToRank = page.GetLeafItemToRank();

            _logger.LogInformation("Starting scan of {LeafCount} leaves from ({Min:O}, {Max:O}].", items.Count, scan.Min, scan.Max);

            return CreateLeafScans(scan, items, leafItemToRank);
        }

        private List<CatalogLeafScan> CreateLeafScans(CatalogPageScan scan, IReadOnlyList<ICatalogLeafItem> items, Dictionary<ICatalogLeafItem, int> leafItemToRank)
        {
            return items
                .OrderBy(x => leafItemToRank[x])
                .Select(x => new CatalogLeafScan(
                    scan.StorageSuffix,
                    scan.GetScanId(),
                    scan.GetPageId(),
                    "L" + leafItemToRank[x].ToString(CultureInfo.InvariantCulture).PadLeft(10, '0'))
                {
                    DriverType = scan.DriverType,
                    DriverParameters = scan.DriverParameters,
                    Url = x.Url,
                    PageUrl = scan.Url,
                    LeafType = x.Type,
                    CommitId = x.CommitId,
                    CommitTimestamp = x.CommitTimestamp,
                    PackageId = x.PackageId,
                    PackageVersion = x.PackageVersion,
                })
                .ToList();
        }
    }
}

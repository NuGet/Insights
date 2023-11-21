// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Insights.WideEntities;
using NuGet.Insights.Worker.LoadBucketedPackage;
using NuGet.Insights.Worker.LoadSymbolPackageArchive;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.ProcessBucketRange
{
    public class ProcessBucketRangeDriverIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        private const string ProcessBucketRange_WithoutEnqueueDir = nameof(ProcessBucketRange_WithoutEnqueue);
        private const string ProcessBucketRange_WithMultiplePartitionKeyWritesDir = nameof(ProcessBucketRange_WithMultiplePartitionKeyWrites);
        private const string ProcessBucketRange_WithEnqueueDir = nameof(ProcessBucketRange_WithEnqueue);

        [Fact]
        public async Task ProcessBucketRange_WithoutEnqueue()
        {
            var min0 = DateTimeOffset.Parse("2020-11-27T20:58:24.1558179Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T23:41:30.2461308Z", CultureInfo.InvariantCulture);

            var bucketMin = 34;
            var bucketMax = 771;

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, onlyLatestLeaves: null, max1);

            var scanId = new StorageId("aaa", "bbb");
            var indexScan = new CatalogIndexScan(CatalogScanDriverType.LoadSymbolPackageArchive, scanId.ToString(), scanId.Unique)
            {
                Min = min0,
                Max = max1,
            };
            await CatalogScanStorageService.InsertAsync(indexScan);
            await CatalogScanStorageService.InitializeLeafScanTableAsync(indexScan.StorageSuffix);

            var taskStateStorageSuffix = "buckets";
            await TaskStateStorageService.InitializeAsync(taskStateStorageSuffix);
            var taskStateKey = new TaskStateKey(taskStateStorageSuffix, "buckets", "buckets");
            await TaskStateStorageService.AddAsync(taskStateKey);

            // Act
            await TableScanService.StartProcessingBucketRangeAsync(
                taskStateKey,
                bucketMin,
                bucketMax,
                indexScan.DriverType,
                indexScan.ScanId,
                enqueue: false);
            var createdBefore = DateTimeOffset.UtcNow;
            await UpdateAsync(taskStateKey);
            var createdAfter = DateTimeOffset.UtcNow;

            // Assert
            var leafScanTable = await CatalogScanStorageService.GetLeafScanTableAsync(indexScan.StorageSuffix);
            var leafScans = await leafScanTable.QueryAsync<CatalogLeafScan>().ToListAsync();

            var bucketedPackagesTable = await BucketedPackageService.GetTableAsync();
            var allBucketedPackages = await bucketedPackagesTable.QueryAsync<BucketedPackage>().ToListAsync();
            var bucketedPackages = allBucketedPackages.Where(x => x.GetBucket() >= bucketMin && x.GetBucket() <= bucketMax).ToList();

            Assert.Equal(leafScans.Count, bucketedPackages.Count);
            Assert.True(leafScans.Count < allBucketedPackages.Count);

            var pairs = bucketedPackages.OrderBy(x => (x.PackageId, x.PackageVersion))
                .Zip(leafScans.OrderBy(x => (x.PackageId, x.PackageVersion)));

            Assert.All(pairs, pair =>
            {
                var (bp, l) = pair;
                Assert.Equal(indexScan.StorageSuffix, l.StorageSuffix);
                Assert.InRange(l.Created, createdBefore, createdAfter);
                Assert.Equal(indexScan.DriverType, l.DriverType);
                Assert.Equal(indexScan.ScanId, l.ScanId);
                Assert.Equal(indexScan.Min.Value, l.Min);
                Assert.Equal(indexScan.Max.Value, l.Max);
                Assert.Equal($"{bp.PartitionKey}-{bp.PackageId.ToLowerInvariant()}", l.PageId);
                Assert.Equal(bp.Url, l.Url);
                Assert.Equal(bp.PageUrl, l.PageUrl);
                Assert.Equal(bp.LeafType, l.LeafType);
                Assert.Equal(bp.CommitId, l.CommitId);
                Assert.Equal(bp.CommitTimestamp, l.CommitTimestamp);
                Assert.Equal(bp.PackageId, l.PackageId);
                Assert.Equal(bp.PackageVersion, l.PackageVersion);
                Assert.Null(l.NextAttempt);
                Assert.Equal($"{l.ScanId}-{l.PageId}", l.PartitionKey);
                Assert.Equal(bp.ParsePackageVersion().ToNormalizedString().ToLowerInvariant(), l.RowKey);
                Assert.NotEqual(default, l.Timestamp);
                Assert.NotEqual(default, l.ETag);
            });

            await AssertEntityOutputAsync<CatalogLeafScan>(
                leafScanTable,
                Path.Combine(ProcessBucketRange_WithoutEnqueueDir, Step1),
                cleanEntity: x => x.Created = DateTimeOffset.Parse("2023-01-03T00:00:00Z", CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ProcessBucketRange_WithMultiplePartitionKeyWrites()
        {
            var min0 = DateTimeOffset.Parse("2023-04-20T15:28:51.3459132Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2023-04-20T15:29:24.2230035Z", CultureInfo.InvariantCulture);

            var bucketMin = 0;
            var bucketMax = BucketedPackage.BucketCount - 1;

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, onlyLatestLeaves: null, max1);

            var scanId = new StorageId("aaa", "bbb");
            var indexScan = new CatalogIndexScan(CatalogScanDriverType.LoadSymbolPackageArchive, scanId.ToString(), scanId.Unique)
            {
                Min = min0,
                Max = max1,
            };
            await CatalogScanStorageService.InsertAsync(indexScan);
            await CatalogScanStorageService.InitializeLeafScanTableAsync(indexScan.StorageSuffix);

            var taskStateStorageSuffix = "buckets";
            await TaskStateStorageService.InitializeAsync(taskStateStorageSuffix);
            var taskStateKey = new TaskStateKey(taskStateStorageSuffix, "buckets", "buckets");
            await TaskStateStorageService.AddAsync(taskStateKey);

            // Act
            await TableScanService.StartProcessingBucketRangeAsync(
                taskStateKey,
                bucketMin,
                bucketMax,
                indexScan.DriverType,
                indexScan.ScanId,
                enqueue: false,
                takeCount: 1);
            await UpdateAsync(taskStateKey);

            // Assert
            var leafScanTable = await CatalogScanStorageService.GetLeafScanTableAsync(indexScan.StorageSuffix);
            await AssertEntityOutputAsync<CatalogLeafScan>(
                leafScanTable,
                Path.Combine(ProcessBucketRange_WithMultiplePartitionKeyWritesDir, Step1),
                cleanEntity: x => x.Created = DateTimeOffset.Parse("2023-01-03T00:00:00Z", CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ProcessBucketRange_WithEnqueue()
        {
            var min0 = DateTimeOffset.Parse("2020-11-27T20:58:24.1558179Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T23:41:30.2461308Z", CultureInfo.InvariantCulture);

            var bucketMin = 34;
            var bucketMax = 771;

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, onlyLatestLeaves: null, max1);

            var scanId = new StorageId("aaa", "bbb");
            var indexScan = new CatalogIndexScan(CatalogScanDriverType.LoadSymbolPackageArchive, scanId.ToString(), scanId.Unique)
            {
                Min = min0,
                Max = max1,
            };
            await CatalogScanStorageService.InsertAsync(indexScan);
            await CatalogScanStorageService.InitializeLeafScanTableAsync(indexScan.StorageSuffix);

            var taskStateStorageSuffix = "buckets";
            await TaskStateStorageService.InitializeAsync(taskStateStorageSuffix);
            var taskStateKey = new TaskStateKey(taskStateStorageSuffix, "buckets", "buckets");
            await TaskStateStorageService.AddAsync(taskStateKey);

            await Host.Services.GetRequiredService<LoadSymbolPackageArchiveDriver>().InitializeAsync(indexScan);

            var leafScanTable = await CatalogScanStorageService.GetLeafScanTableAsync(indexScan.StorageSuffix);

            // Act
            await TableScanService.StartProcessingBucketRangeAsync(
                taskStateKey,
                bucketMin,
                bucketMax,
                indexScan.DriverType,
                indexScan.ScanId,
                enqueue: true);
            await UpdateAsync(taskStateKey);
            await ProcessQueueAsync(async () => (await leafScanTable.GetEntityCountLowerBoundAsync(TelemetryClient.StartQueryLoopMetrics())) == 0);

            // Assert
            var leafScans = await leafScanTable.QueryAsync<CatalogLeafScan>().ToListAsync();
            Assert.Empty(leafScans);

            var bucketedPackagesTable = await BucketedPackageService.GetTableAsync();
            var allBucketedPackages = await bucketedPackagesTable.QueryAsync<BucketedPackage>().ToListAsync();
            var bucketedPackages = allBucketedPackages.Where(x => x.GetBucket() >= bucketMin && x.GetBucket() <= bucketMax).ToList();

            var symbolEntities = await WideEntityService.RetrieveAsync(Options.Value.SymbolPackageArchiveTableName);

            Assert.Equal(symbolEntities.Count, bucketedPackages.Count);
            Assert.True(symbolEntities.Count < allBucketedPackages.Count);

            var pairs = bucketedPackages.OrderBy(x => (x.PackageId.ToLowerInvariant(), x.ParsePackageVersion().ToNormalizedString().ToLowerInvariant()))
                .Zip(symbolEntities.OrderBy(x => (x.PartitionKey, x.RowKey)));

            Assert.All(pairs, pair =>
            {
                var (bp, se) = pair;
                Assert.Equal(bp.PackageId.ToLowerInvariant(), se.PartitionKey);
                Assert.Equal(bp.ParsePackageVersion().ToNormalizedString().ToLowerInvariant(), se.RowKey);
            });

            await AssertSymbolPackageArchiveTableAsync(ProcessBucketRange_WithEnqueueDir, Step1);
        }

        public BucketedPackageService BucketedPackageService => Host.Services.GetService<BucketedPackageService>();
        public TableScanService TableScanService => Host.Services.GetRequiredService<TableScanService>();
        public LoadSymbolPackageArchiveDriver LoadSymbolPackageArchiveDriver => Host.Services.GetRequiredService<LoadSymbolPackageArchiveDriver>();
        public WideEntityService WideEntityService => Host.Services.GetRequiredService<WideEntityService>();

        public ProcessBucketRangeDriverIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}

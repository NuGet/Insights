// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.LoadBucketedPackage;
using NuGet.Insights.Worker.PackageReadmeToCsv;
using NuGet.Insights.Worker.SymbolPackageArchiveToCsv;
using NuGet.Insights.Worker.SymbolPackageFileToCsv;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessServiceIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public const string TimedReprocess_AllReprocessDriversDir = nameof(TimedReprocess_AllReprocessDrivers);
        public const string TimedReprocess_SameBucketRangesDir = nameof(TimedReprocess_SameBucketRangesDir);
        public const string TimedReprocess_SubsequentBucketRangesDir = nameof(TimedReprocess_SubsequentBucketRanges);

        [Fact]
        public async Task TimedReprocess_AllReprocessDrivers()
        {
            // Arrange
            HttpMessageHandlerFactory.Requests.Limit = int.MaxValue;
            HttpMessageHandlerFactory.RequestAndResponses.Limit = int.MaxValue;
            await CatalogScanService.InitializeAsync();
            var min0 = DateTimeOffset.Parse("2024-04-25T02:12:34.0496440Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2024-04-25T02:13:04.3170295Z", CultureInfo.InvariantCulture);
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            var initialLbp = await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, max1);
            await SetCursorsForTimedProcessDriversAsync(max1);

            await TimedReprocessService.InitializeAsync();

            var buckets = new[] { 177, 178, 402, 541, 756 };
            await SetNextBucketsAsync(buckets);

            // Act
            var run = await TimedReprocessService.StartAsync();
            run = await UpdateAsync(run);

            // Assert

            // verify scans have the right shape
            Assert.Equal(TimedReprocessState.Complete, run.State);
            var reprocessScans = (await TimedReprocessStorageService.GetScansAsync(run.RunId))
                .OrderBy(x => x.DriverType)
                .ToList();
            var batches = TimedReprocessService.GetReprocessBatches();
            Assert.Equal(batches.Sum(b => b.Count), reprocessScans.Count);
            var allScans = (await CatalogScanStorageService.GetIndexScansAsync())
                .Where(x => x.ScanId != initialLbp.ScanId)
                .OrderBy(x => x.DriverType)
                .ToList();
            Assert.Equal(reprocessScans.Count, allScans.Count);
            Assert.All(reprocessScans.Zip(allScans), tuple =>
            {
                var (reprocessScan, indexScan) = tuple;
                Assert.True(reprocessScan.Completed);
                Assert.Equal(CatalogIndexScanState.Complete, indexScan.State);
                Assert.Equal("177-178,402,541,756", indexScan.BucketRanges);
                Assert.Equal(indexScan.DriverType, reprocessScan.DriverType);
                Assert.Equal(indexScan.ScanId, reprocessScan.ScanId);
                Assert.Equal(indexScan.StorageSuffix, reprocessScan.StorageSuffix);
            });

            // verify the ordering of the catalog scans (batches didn't overlap start times)
            var batchedScans = batches
                .Select(batch => batch.Select(type => allScans.Single(s => s.DriverType == type)).ToList())
                .ToList();
            for (var i = 0; i < batchedScans.Count - 1; i++)
            {
                var endOfCurrent = batchedScans[i].Max(x => x.Completed.Value);
                var startOfNext = batchedScans[i + 1].Min(x => x.Created);
                Assert.True(endOfCurrent <= startOfNext);
            }

            // verify output data
            await AssertPackageReadmeTableAsync(TimedReprocess_AllReprocessDriversDir, Step1, "PackageReadmes.json");
            await AssertSymbolPackageHashesTableAsync(TimedReprocess_AllReprocessDriversDir, Step1, "SymbolPackageHashes.json");
            await AssertSymbolPackageArchiveTableAsync(TimedReprocess_AllReprocessDriversDir, Step1, "SymbolPackageArchives.json");

            await AssertCsvAsync<PackageReadme>(Options.Value.PackageReadmeContainerName, TimedReprocess_AllReprocessDriversDir, Step1, "PackageReadmes.csv");
            await AssertCsvAsync<SymbolPackageFileRecord>(Options.Value.SymbolPackageFileContainerName, TimedReprocess_AllReprocessDriversDir, Step1, "SymbolPackageFiles.csv");
            await AssertCsvAsync<SymbolPackageArchiveRecord>(Options.Value.SymbolPackageArchiveContainerName, TimedReprocess_AllReprocessDriversDir, Step1, "SymbolPackageArchives.csv");
            await AssertCsvAsync<SymbolPackageArchiveEntry>(Options.Value.SymbolPackageArchiveEntryContainerName, TimedReprocess_AllReprocessDriversDir, Step1, "SymbolPackageArchiveEntries.csv");

            var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync(Options.Value);
            var tables = await tableServiceClient.QueryAsync(prefix: StoragePrefix).ToListAsync();
            Assert.Equal(
                new string[]
                {
                    // infrastructure
                    Options.Value.BucketedPackageTableName,
                    Options.Value.CatalogIndexScanTableName,
                    Options.Value.CursorTableName,
                    Options.Value.TimedReprocessTableName,

                    // intermediate data
                    Options.Value.PackageReadmeTableName,
                    Options.Value.SymbolPackageArchiveTableName,
                    Options.Value.SymbolPackageHashesTableName,
                }.Order().ToArray(),
                tables.Select(x => x.Name).ToArray());

            var blobServiceClient = await ServiceClientFactory.GetBlobServiceClientAsync(Options.Value);
            var containers = await blobServiceClient.GetBlobContainersAsync(prefix: StoragePrefix).ToListAsync();
            Assert.Equal(
                new string[]
                {
                    // infrastructure
                    Options.Value.LeaseContainerName,

                    // intermediate data
                    Options.Value.PackageReadmeContainerName,
                    Options.Value.SymbolPackageArchiveEntryContainerName,
                    Options.Value.SymbolPackageArchiveContainerName,
                    Options.Value.SymbolPackageFileContainerName,
                }.Order().ToArray(),
                containers.Select(x => x.Name).ToArray());
        }

        [Fact]
        public async Task TimedReprocess_WaitForCursorBasedScan()
        {
            // Arrange
            await CatalogScanService.InitializeAsync();
            var min0 = DateTimeOffset.Parse("2024-04-25T02:12:34.0496440Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2024-04-25T02:13:04.3170295Z", CultureInfo.InvariantCulture);
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            var initialLbp = await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, max1);

            await SetCursorAsync(CatalogScanDriverType.LoadSymbolPackageArchive, min0);
            var parallelLspaResult = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadSymbolPackageArchive, max1);

            await SetCursorsAsync([
                    CatalogScanDriverType.LoadPackageReadme,
                CatalogScanDriverType.PackageReadmeToCsv,
                CatalogScanDriverType.LoadSymbolPackageArchive,
                CatalogScanDriverType.SymbolPackageFileToCsv,
                CatalogScanDriverType.SymbolPackageArchiveToCsv
                ],
                max1);

            await TimedReprocessService.InitializeAsync();

            var buckets = new[] { 177, 178, 402, 541, 756 };
            await SetNextBucketsAsync(buckets);

            // Act
            var run = await TimedReprocessService.StartAsync();
            run = await UpdateAsync(run);
            var parallelLspa = await UpdateAsync(parallelLspaResult);

            // Assert
            Assert.Equal(TimedReprocessState.Complete, run.State);
            var reprocessScans = (await TimedReprocessStorageService.GetScansAsync(run.RunId))
                .OrderBy(x => x.DriverType)
                .ToList();
            var batches = TimedReprocessService.GetReprocessBatches();
            Assert.Equal(batches.Sum(b => b.Count), reprocessScans.Count);
            var allScans = (await CatalogScanStorageService.GetIndexScansAsync())
                .Where(x => x.ScanId != initialLbp.ScanId)
                .OrderBy(x => x.DriverType)
                .ThenBy(x => x.Created)
                .ToList();
            var lspa = allScans.Where(x => x.DriverType == CatalogScanDriverType.LoadSymbolPackageArchive).ToList();
            Assert.Equal(2, lspa.Count);
            Assert.Equal(min0, lspa[0].Min);
            Assert.Equal(max1, lspa[0].Max);
            Assert.Null(lspa[0].BucketRanges);
            Assert.Equal(CatalogClient.NuGetOrgMinDeleted, lspa[1].Min);
            Assert.Equal(max1, lspa[1].Max);
            Assert.Equal("177-178,402,541,756", lspa[1].BucketRanges);
            Assert.True(lspa[0].Completed <= lspa[1].Created);
        }

        [Fact]
        public async Task TimedReprocess_SameBucketRanges()
        {
            // Arrange
            ConfigureWorkerSettings = x =>
            {
                x.DisabledDrivers = [CatalogScanDriverType.LoadSymbolPackageArchive, CatalogScanDriverType.SymbolPackageFileToCsv, CatalogScanDriverType.SymbolPackageArchiveToCsv];
            };

            await CatalogScanService.InitializeAsync();
            var min0 = DateTimeOffset.Parse("2024-04-25T02:12:34.0496440Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2024-04-25T02:13:04.3170295Z", CultureInfo.InvariantCulture);
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, max1);
            await SetCursorsAsync([CatalogScanDriverType.LoadPackageReadme, CatalogScanDriverType.PackageReadmeToCsv], max1);

            await TimedReprocessService.InitializeAsync();

            var buckets = new[] { 177, 178, 402, 541, 756 };

            // Act
            var runA = await TimedReprocessService.StartAsync(buckets);
            runA = await UpdateAsync(runA);

            // Assert
            await AssertPackageReadmeTableAsync(TimedReprocess_SameBucketRangesDir, Step1, "PackageReadmes.json");
            await AssertCsvAsync<PackageReadme>(Options.Value.PackageReadmeContainerName, TimedReprocess_SameBucketRangesDir, Step1, "PackageReadmes.csv");
            Assert.Equal("177-178,402,541,756", runA.BucketRanges);

            var metric = TelemetryClient.Metrics[new("CsvRecordStorageService.CompactAsync.BlobChange", "DestContainer", "RecordType", "Bucket")];
            var value = Assert.Single(metric.MetricValues);
            Assert.Equal(1, value.MetricValue);
            metric.MetricValues.Clear();

            // Act
            var runB = await TimedReprocessService.StartAsync(buckets);
            runB = await UpdateAsync(runB);

            // Assert
            await AssertPackageReadmeTableAsync(TimedReprocess_SameBucketRangesDir, Step1, "PackageReadmes.json"); // data is unchanged
            await AssertCsvAsync<PackageReadme>(Options.Value.PackageReadmeContainerName, TimedReprocess_SameBucketRangesDir, Step1, "PackageReadmes.csv"); // data is unchanged
            Assert.Equal("177-178,402,541,756", runA.BucketRanges);

            value = Assert.Single(metric.MetricValues);
            Assert.Equal(0, value.MetricValue);
        }

        [Fact]
        public async Task TimedReprocess_SubsequentBucketRanges()
        {
            // Arrange
            ConfigureWorkerSettings = x =>
            {
                x.DisabledDrivers = [CatalogScanDriverType.LoadSymbolPackageArchive, CatalogScanDriverType.SymbolPackageFileToCsv, CatalogScanDriverType.SymbolPackageArchiveToCsv];
            };

            await CatalogScanService.InitializeAsync();
            var min0 = DateTimeOffset.Parse("2024-04-25T02:12:34.0496440Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2024-04-25T02:13:04.3170295Z", CultureInfo.InvariantCulture);
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, max1);
            await SetCursorsAsync([CatalogScanDriverType.LoadPackageReadme, CatalogScanDriverType.PackageReadmeToCsv], max1);

            await TimedReprocessService.InitializeAsync();

            var bucketsA = new[] { 177, 178, 402 };
            var bucketsB = new[] { 541, 756 };

            // Act
            var runA = await TimedReprocessService.StartAsync(bucketsA);
            runA = await UpdateAsync(runA);

            // Assert
            await AssertPackageReadmeTableAsync(TimedReprocess_SubsequentBucketRangesDir, Step1, "PackageReadmes.json");
            await AssertCsvAsync<PackageReadme>(Options.Value.PackageReadmeContainerName, TimedReprocess_SubsequentBucketRangesDir, Step1, "PackageReadmes.csv");
            Assert.Equal("177-178,402", runA.BucketRanges);

            // Act
            var runB = await TimedReprocessService.StartAsync(bucketsB);
            runB = await UpdateAsync(runB);

            // Assert
            await AssertPackageReadmeTableAsync(TimedReprocess_SubsequentBucketRangesDir, Step2, "PackageReadmes.json");
            await AssertCsvAsync<PackageReadme>(Options.Value.PackageReadmeContainerName, TimedReprocess_SubsequentBucketRangesDir, Step2, "PackageReadmes.csv");
            Assert.Equal("541,756", runB.BucketRanges);
        }

        [Fact]
        public async Task TimedReprocess_ReturnsCompletedRunWhenCaughtUp()
        {
            // Arrange
            ConfigureWorkerSettings = x =>
            {
                x.DisabledDrivers = [CatalogScanDriverType.LoadSymbolPackageArchive, CatalogScanDriverType.SymbolPackageFileToCsv, CatalogScanDriverType.SymbolPackageArchiveToCsv];
            };

            await CatalogScanService.InitializeAsync();
            var min0 = DateTimeOffset.Parse("2024-04-25T02:12:34.0496440Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2024-04-25T02:13:04.3170295Z", CultureInfo.InvariantCulture);
            await SetCursorsAsync(
                [CatalogScanDriverType.LoadBucketedPackage, CatalogScanDriverType.LoadPackageReadme, CatalogScanDriverType.PackageReadmeToCsv],
                min0);
            await UpdateInBatchesAsync(
                [CatalogScanDriverType.LoadBucketedPackage, CatalogScanDriverType.LoadPackageReadme, CatalogScanDriverType.PackageReadmeToCsv],
                max1);

            await TimedReprocessService.InitializeAsync();

            var buckets = new[] { 402 };
            var latestRun = await TimedReprocessService.StartAsync(buckets);
            latestRun = await UpdateAsync(latestRun);

            await SetNextBucketsAsync(Array.Empty<int>());

            // Act
            var started = await TimedReprocessService.StartAsync();

            // Assert
            Assert.Equal(TimedReprocessState.Complete, started.State);
            Assert.Equal(latestRun.RunId, started.RunId);
        }

        [Fact]
        public async Task TimedReprocess_BlocksWhenCursorsAreNotAligned()
        {
            // Arrange
            AssertLogLevel = LogLevel.Error;
            ConfigureWorkerSettings = x =>
            {
                x.DisabledDrivers = [CatalogScanDriverType.LoadSymbolPackageArchive, CatalogScanDriverType.SymbolPackageFileToCsv, CatalogScanDriverType.SymbolPackageArchiveToCsv];
            };

            await CatalogScanService.InitializeAsync();
            var min0 = DateTimeOffset.Parse("2024-04-25T02:12:34.0496440Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2024-04-25T02:13:04.3170295Z", CultureInfo.InvariantCulture);
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            var initialLbp = await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, max1);
            await SetCursorAsync(CatalogScanDriverType.LoadPackageReadme, min0);
            await SetCursorAsync(CatalogScanDriverType.PackageReadmeToCsv, min0);

            await TimedReprocessService.InitializeAsync();

            // Act
            var run = await TimedReprocessService.StartAsync();

            // Assert
            Assert.Null(run);
            Assert.Contains(LogMessages, x => x.Contains("The drivers to reprocess do not have aligned cursors.", StringComparison.Ordinal));
        }

        private async Task SetCursorsForTimedProcessDriversAsync(DateTimeOffset min)
        {
            await SetCursorsAsync(TimedReprocessService.GetReprocessBatches().SelectMany(x => x), min);
        }

        private async Task SetNextBucketsAsync(IReadOnlyList<int> buckets)
        {
            var otherBuckets = Enumerable.Range(0, BucketedPackage.BucketCount).Except(buckets);
            UtcNow = DateTimeOffset.UtcNow - (3 * Options.Value.TimedReprocessWindow);
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(buckets);
            UtcNow = DateTimeOffset.UtcNow + (3 * Options.Value.TimedReprocessWindow);
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(otherBuckets);
            UtcNow = null;
        }

        private async Task<string> AssertCsvAsync<T>(string containerName, string testName, string stepName, string fileName) where T : ICsvRecord
        {
            return await AssertCsvAsync<T>(containerName, testName, stepName, 0, fileName);
        }

        public TimedReprocessServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}

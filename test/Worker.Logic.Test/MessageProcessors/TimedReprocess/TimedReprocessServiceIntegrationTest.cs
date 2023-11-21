// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Insights.Worker.LoadBucketedPackage;
using NuGet.Insights.Worker.PackageReadmeToCsv;
using NuGet.Insights.Worker.SymbolPackageArchiveToCsv;
using Xunit;
using Xunit.Abstractions;

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
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

            await CatalogScanService.InitializeAsync();
            var min0 = DateTimeOffset.Parse("2018-12-06T03:17:32.1388561Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2018-12-06T03:17:41.9986142Z", CultureInfo.InvariantCulture);
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            var initialLbp = await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, max1);
            await SetCursorsAsync([CatalogScanDriverType.LoadPackageReadme, CatalogScanDriverType.LoadSymbolPackageArchive], max1);

            await TimedReprocessService.InitializeAsync();

            var buckets = new[] { 375, 401, 826, 827, 828, 829 };
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
            Assert.All(reprocessScans.Zip(allScans), tuple =>
            {
                var (reprocessScan, indexScan) = tuple;
                Assert.True(reprocessScan.Completed);
                Assert.Equal(CatalogIndexScanState.Complete, indexScan.State);
                Assert.Equal("375,401,826-829", indexScan.BucketRanges);
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
            await AssertPackageReadmeTableAsync(TimedReprocess_AllReprocessDriversDir, Step1, "package-readmes.json");
            await AssertSymbolPackageArchiveTableAsync(TimedReprocess_AllReprocessDriversDir, Step1, "symbol-package-archives.json");

            await AssertCompactAsync<PackageReadme>(Options.Value.PackageReadmeContainerName, TimedReprocess_AllReprocessDriversDir, Step1, "package-readmes.csv");
            await AssertCompactAsync<SymbolPackageArchiveRecord>(Options.Value.SymbolPackageArchiveContainerName, TimedReprocess_AllReprocessDriversDir, Step1, "symbol-package-archives.csv");
            await AssertCompactAsync<SymbolPackageArchiveEntry>(Options.Value.SymbolPackageArchiveEntryContainerName, TimedReprocess_AllReprocessDriversDir, Step1, "symbol-package-archive-entries.csv");
        }

        [Fact]
        public async Task TimedReprocess_WaitForCursorBasedScan()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

            await CatalogScanService.InitializeAsync();
            var min0 = DateTimeOffset.Parse("2018-12-06T03:17:32.1388561Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2018-12-06T03:17:41.9986142Z", CultureInfo.InvariantCulture);
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            var initialLbp = await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, max1);

            await SetCursorAsync(CatalogScanDriverType.LoadSymbolPackageArchive, min0);
            var parallelLspa = (await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadSymbolPackageArchive, max1)).Scan;

            await SetCursorsAsync([CatalogScanDriverType.LoadPackageReadme, CatalogScanDriverType.LoadSymbolPackageArchive], max1);

            await TimedReprocessService.InitializeAsync();

            var buckets = new[] { 375, 401, 826, 827, 828, 829 };
            await SetNextBucketsAsync(buckets);

            // Act
            var run = await TimedReprocessService.StartAsync();
            run = await UpdateAsync(run);
            parallelLspa = await UpdateAsync(parallelLspa);

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
            Assert.Equal("375,401,826-829", lspa[1].BucketRanges);
            Assert.True(lspa[0].Completed <= lspa[1].Created);
        }

        [Fact]
        public async Task TimedReprocess_SameBucketRanges()
        {
            // Arrange
            ConfigureWorkerSettings = x =>
            {
                x.AppendResultStorageBucketCount = 1;
                x.DisabledDrivers = [CatalogScanDriverType.LoadSymbolPackageArchive, CatalogScanDriverType.SymbolPackageArchiveToCsv];
            };

            await CatalogScanService.InitializeAsync();
            var min0 = DateTimeOffset.Parse("2018-12-06T03:17:32.1388561Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2018-12-06T03:17:41.9986142Z", CultureInfo.InvariantCulture);
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            var initialLbp = await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, max1);
            await SetCursorAsync(CatalogScanDriverType.LoadPackageReadme, max1);

            await TimedReprocessService.InitializeAsync();

            var buckets = new[] { 375, 401, 826, 827, 828, 829 };

            // Act
            var runA = await TimedReprocessService.StartAsync(buckets);
            runA = await UpdateAsync(runA);

            // Assert
            await AssertPackageReadmeTableAsync(TimedReprocess_SameBucketRangesDir, Step1, "package-readmes.json");
            await AssertCompactAsync<PackageReadme>(Options.Value.PackageReadmeContainerName, TimedReprocess_SameBucketRangesDir, Step1, "package-readmes.csv");
            Assert.Equal("375,401,826-829", runA.BucketRanges);

            var metric = TelemetryClient.Metrics[new("AppendResultStorageService.CompactAsync.BlobChange", "DestContainer", "RecordType")];
            var value = Assert.Single(metric.MetricValues);
            Assert.Equal(1, value.MetricValue);
            metric.MetricValues.Clear();

            // Act
            var runB = await TimedReprocessService.StartAsync(buckets);
            runB = await UpdateAsync(runB);

            // Assert
            await AssertPackageReadmeTableAsync(TimedReprocess_SameBucketRangesDir, Step1, "package-readmes.json"); // data is unchanged
            await AssertCompactAsync<PackageReadme>(Options.Value.PackageReadmeContainerName, TimedReprocess_SameBucketRangesDir, Step1, "package-readmes.csv"); // data is unchanged
            Assert.Equal("375,401,826-829", runA.BucketRanges);

            value = Assert.Single(metric.MetricValues);
            Assert.Equal(0, value.MetricValue);
        }

        [Fact]
        public async Task TimedReprocess_SubsequentBucketRanges()
        {
            // Arrange
            ConfigureWorkerSettings = x =>
            {
                x.AppendResultStorageBucketCount = 1;
                x.DisabledDrivers = [CatalogScanDriverType.LoadSymbolPackageArchive, CatalogScanDriverType.SymbolPackageArchiveToCsv];
            };

            await CatalogScanService.InitializeAsync();
            var min0 = DateTimeOffset.Parse("2018-12-06T03:17:32.1388561Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2018-12-06T03:17:41.9986142Z", CultureInfo.InvariantCulture);
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            var initialLbp = await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, max1);
            await SetCursorAsync(CatalogScanDriverType.LoadPackageReadme, max1);

            await TimedReprocessService.InitializeAsync();

            var bucketsA = new[] { 375, 401, 826 };
            var bucketsB = new[] { 827, 828, 829 };

            // Act
            var runA = await TimedReprocessService.StartAsync(bucketsA);
            runA = await UpdateAsync(runA);

            // Assert
            await AssertPackageReadmeTableAsync(TimedReprocess_SubsequentBucketRangesDir, Step1, "package-readmes.json");
            await AssertCompactAsync<PackageReadme>(Options.Value.PackageReadmeContainerName, TimedReprocess_SubsequentBucketRangesDir, Step1, "package-readmes.csv");
            Assert.Equal("375,401,826", runA.BucketRanges);

            // Act
            var runB = await TimedReprocessService.StartAsync(bucketsB);
            runB = await UpdateAsync(runB);

            // Assert
            await AssertPackageReadmeTableAsync(TimedReprocess_SubsequentBucketRangesDir, Step2, "package-readmes.json");
            await AssertCompactAsync<PackageReadme>(Options.Value.PackageReadmeContainerName, TimedReprocess_SubsequentBucketRangesDir, Step2, "package-readmes.csv");
            Assert.Equal("827-829", runB.BucketRanges);
        }

        [Fact]
        public async Task TimedReprocess_ReturnsCompletedRunWhenCaughtUp()
        {
            // Arrange
            ConfigureWorkerSettings = x =>
            {
                x.AppendResultStorageBucketCount = 1;
                x.DisabledDrivers = [CatalogScanDriverType.LoadSymbolPackageArchive, CatalogScanDriverType.SymbolPackageArchiveToCsv];
            };

            await CatalogScanService.InitializeAsync();
            var min0 = DateTimeOffset.Parse("2018-12-06T03:17:32.1388561Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2018-12-06T03:17:41.9986142Z", CultureInfo.InvariantCulture);
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            var initialLbp = await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, max1);
            await SetCursorAsync(CatalogScanDriverType.LoadPackageReadme, max1);

            await TimedReprocessService.InitializeAsync();

            var buckets = new[] { 375 };
            var latestRun = await TimedReprocessService.StartAsync(buckets);
            latestRun = await UpdateAsync(latestRun);

            await SetNextBucketsAsync(Array.Empty<int>());

            // Act
            var started = await TimedReprocessService.StartAsync();

            // Assert
            Assert.Equal(TimedReprocessState.Complete, started.State);
            Assert.Equal(latestRun.RunId, started.RunId);
        }

        private async Task SetNextBucketsAsync(IReadOnlyList<int> buckets)
        {
            var otherBuckets = Enumerable.Range(0, BucketedPackage.BucketCount).Except(buckets);
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(buckets, -2 * Options.Value.TimedReprocessWindow);
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(otherBuckets, 2 * Options.Value.TimedReprocessWindow);
        }

        private async Task<string> AssertCompactAsync<T>(string containerName, string testName, string stepName, string fileName) where T : ICsvRecord
        {
            return await AssertCompactAsync<T>(containerName, testName, stepName, 0, fileName);
        }

        public TimedReprocessServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}

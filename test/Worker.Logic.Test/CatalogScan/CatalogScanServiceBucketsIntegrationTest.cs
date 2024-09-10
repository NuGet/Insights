// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Frozen;
using NuGet.Insights.Worker.LoadBucketedPackage;
using NuGet.Insights.Worker.PackageSignatureToCsv;

namespace NuGet.Insights.Worker
{
    public class CatalogScanServiceIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public class PositiveTests : CatalogScanServiceIntegrationTest
        {
            private const string CatalogScanService_UpdateWithBucketRangesDir = nameof(CatalogScanService_UpdateWithBucketRanges);

            [Fact]
            public async Task CatalogScanService_UpdateWithBucketRanges()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2024-04-25T02:12:34.0496440Z", CultureInfo.InvariantCulture);
                var max1 = DateTimeOffset.Parse("2024-04-25T02:13:04.3170295Z", CultureInfo.InvariantCulture);
                var driverType = CatalogScanDriverType.PackageSignatureToCsv;
                var scanId = "my-scan-id";
                var storageSuffix = "zz";
                var buckets = new[] { 177, 178, 402, 541, 756 };

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
                await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, max1);
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
                var cursorBefore = await SetCursorAsync(driverType, max1);

                // Act
                var result = await CatalogScanService.UpdateAsync(scanId, storageSuffix, driverType, buckets);
                Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
                await UpdateAsync(result);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
                Assert.Equal(scanId, result.Scan.ScanId);
                Assert.Equal(storageSuffix, result.Scan.StorageSuffix);
                var scan = await CatalogScanStorageService.GetIndexScanAsync(driverType, scanId);
                Assert.Equal(CatalogIndexScanState.Complete, scan.State);
                var cursorAfter = await CatalogScanCursorService.GetCursorAsync(driverType);
                Assert.Equal(max1, cursorAfter.Value);
                Assert.Equal(cursorBefore.ETag, cursorAfter.ETag);

                var csvContent = await AssertCsvAsync<PackageSignature>(Options.Value.PackageSignatureContainerName, CatalogScanService_UpdateWithBucketRangesDir, Step1, 0);

                var bucketedPackages = (await GetEntitiesAsync<BucketedPackage>(Options.Value.BucketedPackageTableName))
                    .Where(x => buckets.Contains(x.GetBucket()))
                    .OrderBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.PackageVersion, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var packageArchiveEntities = (await GetWideEntitiesAsync<PackageFileService.PackageFileInfoVersions>(Options.Value.PackageArchiveTableName))
                    .OrderBy(x => x.PartitionKey, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.RowKey, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var csvLines = csvContent.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).Skip(1).ToList();

                Assert.Equal(5, bucketedPackages.Count);
                Assert.Equal(5, packageArchiveEntities.Count);
                Assert.Equal(5, csvLines.Count);
                Assert.All(bucketedPackages.Zip(packageArchiveEntities, csvLines), tuple =>
                {
                    var (bp, pa, csvLine) = tuple;
                    Assert.Equal(bp.PackageId.ToLowerInvariant(), pa.PartitionKey);
                    Assert.Equal(bp.ParsePackageVersion().ToNormalizedString().ToLowerInvariant(), pa.RowKey);
                    Assert.StartsWith($",,{pa.PartitionKey},{pa.PartitionKey}/{pa.RowKey},", csvLine, StringComparison.Ordinal);
                });
            }

            [Fact]
            public async Task AllDrivers_UpdateWithCatalogRange_FindLatest()
            {
                var driversUnderTest = CatalogScanDriverMetadata.StartableDriverTypes
                    .Where(x => CatalogScanDriverMetadata.GetOnlyLatestLeavesSupport(x).GetValueOrDefault(true))
                    .ToHashSet();

                var scans = await RunAllDriversAsync(
                    driversUnderTest,
                    async () =>
                    {
                        await CatalogScanService.InitializeAsync();
                        await SetCursorsAsync(CatalogScanDriverMetadata.StartableDriverTypes, Min0);
                    },
                    async driverType =>
                    {
                        bool? onlyLatestLeaves = driversUnderTest.Contains(driverType) ? true : null;
                        return await CatalogScanService.UpdateAsync(driverType, Max1, onlyLatestLeaves);
                    });

                Assert.All(driversUnderTest, x => Assert.True(scans[x][0].OnlyLatestLeaves));
            }

            [Fact]
            public async Task AllDrivers_UpdateWithCatalogRange_AllLeaves()
            {
                var driversUnderTest = CatalogScanDriverMetadata.StartableDriverTypes
                    .Where(x => !CatalogScanDriverMetadata.GetOnlyLatestLeavesSupport(x).GetValueOrDefault(false))
                    .ToHashSet();

                var scans = await RunAllDriversAsync(
                    driversUnderTest,
                    async () =>
                    {
                        await CatalogScanService.InitializeAsync();
                        await SetCursorsAsync(CatalogScanDriverMetadata.StartableDriverTypes, Min0);
                    },
                    async driverType =>
                    {
                        bool? onlyLatestLeaves = driversUnderTest.Contains(driverType) ? false : null;
                        return await CatalogScanService.UpdateAsync(driverType, Max1, onlyLatestLeaves);
                    });

                Assert.All(driversUnderTest, x => Assert.False(scans[x][0].OnlyLatestLeaves));
            }

            [Fact]
            public async Task AllDrivers_UpdateWithBuckets()
            {
                var buckets = Enumerable.Range(0, BucketedPackage.BucketCount).ToList();
                var driversUnderTest = CatalogScanDriverMetadata.StartableDriverTypes
                    .Where(CatalogScanDriverMetadata.GetBucketRangeSupport)
                    .ToHashSet();

                var scans = await RunAllDriversAsync(
                    driversUnderTest,
                    async () =>
                    {
                        await CatalogScanService.InitializeAsync();
                        await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, Min0);
                        await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, Max1);
                        await SetCursorsAsync(CatalogScanDriverMetadata.StartableDriverTypes, Max1);
                    },
                    async driverType =>
                    {
                        if (driversUnderTest.Contains(driverType))
                        {
                            return await CatalogScanService.UpdateAsync(driverType, buckets);
                        }
                        else
                        {
                            await SetCursorAsync(driverType, Min0);
                            var result = await CatalogScanService.UpdateAsync(driverType, Max1);
                            await SetCursorAsync(driverType, Max1);
                            return result;
                        }
                    });

                Assert.All(driversUnderTest, x => Assert.True(scans[x][0].OnlyLatestLeaves));
                Assert.All(driversUnderTest, x => Assert.Equal("0-999", scans[x][0].BucketRanges));
            }

            public PositiveTests(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public class CachingTests : CatalogScanServiceIntegrationTest
        {
            [Fact]
            public async Task CatalogScanService_BucketRangeUpdatesDataCachedByBucketRange()
            {
                await CatalogScanService_TestCachedData(bucketRangeFirst: true, bucketRangeSecond: true, expectCached: false, appendCsvError: false);
            }

            [Fact]
            public async Task CatalogScanService_BucketRangeUpdatesDataCachedByCatalogRange()
            {
                await CatalogScanService_TestCachedData(bucketRangeFirst: false, bucketRangeSecond: true, expectCached: false, appendCsvError: false);
            }

            [Fact]
            public async Task CatalogScanService_CatalogRangeUpdatesDataCachedByBucketRange()
            {
                await CatalogScanService_TestCachedData(bucketRangeFirst: true, bucketRangeSecond: false, expectCached: false, appendCsvError: false);
            }

            [Fact]
            public async Task CatalogScanService_CatalogRangeUsesDataCachedByCatalogRange()
            {
                await CatalogScanService_TestCachedData(bucketRangeFirst: false, bucketRangeSecond: false, expectCached: true, appendCsvError: false);
            }

            public CachingTests(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public class AppendCsvErrorTests : CatalogScanServiceIntegrationTest
        {
            [Fact]
            public async Task CatalogScanService_BucketRangeUpdatesDataCachedByBucketRange_WithAppendCsvError()
            {
                await CatalogScanService_TestCachedData(bucketRangeFirst: true, bucketRangeSecond: true, expectCached: false, appendCsvError: true);
            }

            [Fact]
            public async Task CatalogScanService_BucketRangeUpdatesDataCachedByCatalogRange_WithAppendCsvError()
            {
                await CatalogScanService_TestCachedData(bucketRangeFirst: false, bucketRangeSecond: true, expectCached: false, appendCsvError: true);
            }

            [Fact]
            public async Task CatalogScanService_CatalogRangeUpdatesDataCachedByBucketRange_WithAppendCsvError()
            {
                await CatalogScanService_TestCachedData(bucketRangeFirst: true, bucketRangeSecond: false, expectCached: false, appendCsvError: true);
            }

            [Fact]
            public async Task CatalogScanService_CatalogRangeUsesDataCachedByCatalogRange_WithAppendCsvError()
            {
                await CatalogScanService_TestCachedData(bucketRangeFirst: false, bucketRangeSecond: false, expectCached: true, appendCsvError: true);
            }

            public AppendCsvErrorTests(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        private async Task<int> GetMinimumLeafAttemptCount(string storageSuffix)
        {
            var indexScans = await CatalogScanStorageService.GetIndexScansAsync();
            foreach (var indexScan in indexScans)
            {
                if (indexScan.StorageSuffix != storageSuffix
                    || indexScan.State == CatalogIndexScanState.Created
                    || indexScan.State >= CatalogIndexScanState.StartingAggregate)
                {
                    continue;
                }

                var leafScans = await CatalogScanStorageService.GetLeafScansAsync(indexScan.StorageSuffix);
                return leafScans.Min(x => x.AttemptCount);
            }

            throw new InvalidOperationException();
        }

        private async Task CatalogScanService_TestCachedData(bool bucketRangeFirst, bool bucketRangeSecond, bool expectCached, bool appendCsvError)
        {
            // Arrange
            RetryFailedMessages = appendCsvError;
            ConfigureWorkerSettings = x =>
            {
                if (appendCsvError)
                {
                    // retry at the queue message level instead
                    x.AzureServiceClientMaxRetries = 0;
                    x.AzureServiceClientNetworkTimeout = TimeSpan.FromSeconds(10);
                }

                x.OldCatalogIndexScansToKeep = 0;
            };
            var dir = nameof(CatalogScanService_TestCachedData);
            var min0 = DateTimeOffset.Parse("2020-05-11T20:11:38.5525171Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-05-11T20:12:56.9143404Z", CultureInfo.InvariantCulture);
            var bucket = 0;
            var buckets = Enumerable.Range(0, BucketedPackage.BucketCount).ToList();
            var driversUnderTest = TimedReprocessService
                .GetReprocessBatches()
                .SelectMany(x => x)
                .ToHashSet();
            var recordTypes = driversUnderTest
                .SelectMany(x => CatalogScanDriverMetadata.GetRecordTypes(x) ?? Enumerable.Empty<Type>())
                .ToList();

            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                // simulate an error while writing out CSV records, using cached data should reproduce the records
                if (appendCsvError
                    && r.Method != HttpMethod.Get
                    && r.RequestUri.AbsolutePath.Contains("/" + Options.Value.CsvRecordTableName, StringComparison.Ordinal))
                {
                    // CSV record table format: CsvRecordTableName + storage suffix + set index (digit)
                    var storageSuffix = r.RequestUri.AbsolutePath.Split(Options.Value.CsvRecordTableName)[1];
                    storageSuffix = storageSuffix.Substring(0, storageSuffix.Length - 1);
                    if (await GetMinimumLeafAttemptCount(storageSuffix) == 1)
                    {
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                        {
                            RequestMessage = r,
                            Content = new StringContent("Big yikes!"),
                        };
                    }
                }

                if (r.RequestUri.AbsolutePath.EndsWith("/readme", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        RequestMessage = r,
                        Content = new StringContent("# My package"),
                    };
                }

                return null;
            };

            // Act
            await RunAllDriversAsync(
                driversUnderTest,
                async () =>
                {
                    await CatalogScanService.InitializeAsync();

                    if (bucketRangeFirst || bucketRangeSecond)
                    {
                        await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
                        await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, max1);
                    }

                    await SetCursorsAsync(CatalogScanDriverMetadata.StartableDriverTypes, bucketRangeFirst ? max1 : min0);
                },
                async driverType =>
                {
                    if (bucketRangeFirst)
                    {
                        return await CatalogScanService.UpdateAsync(driverType, buckets);
                    }
                    else
                    {
                        return await CatalogScanService.UpdateAsync(driverType, max1);
                    }
                });

            // Assert
            foreach (var recordType in recordTypes)
            {
                var containerName = CsvRecordContainers.GetContainerName(recordType);
                var tableName = CsvRecordContainers.GetDefaultKustoTableName(recordType);
                await AssertCsvAsync(recordType, containerName, dir, Step1, bucket, $"{tableName}.csv");
            }

            if (!appendCsvError)
            {
                Assert.Equal(1, HttpMessageHandlerFactory.RequestAndResponses.Count(x => x.OriginalRequest.Method == HttpMethod.Head && x.OriginalRequest.RequestUri.AbsolutePath.EndsWith(".snupkg", StringComparison.Ordinal) && x.Response.StatusCode == HttpStatusCode.OK));
                Assert.Equal(1, HttpMessageHandlerFactory.RequestAndResponses.Count(x => x.OriginalRequest.Method == HttpMethod.Get && x.OriginalRequest.RequestUri.AbsolutePath.EndsWith(".snupkg", StringComparison.Ordinal) && x.Response.StatusCode == HttpStatusCode.OK));
                Assert.Equal(1, HttpMessageHandlerFactory.RequestAndResponses.Count(x => x.OriginalRequest.RequestUri.AbsolutePath.EndsWith("/readme", StringComparison.Ordinal) && x.Response.StatusCode == HttpStatusCode.OK));
            }

            // Arrange
            HttpMessageHandlerFactory.OnSendAsync = async (req, b, t) =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/readme", StringComparison.Ordinal))
                {
                    var newReq = Clone(req);
                    newReq.Headers.TryAddWithoutValidation("Original", req.RequestUri.AbsoluteUri);
                    newReq.RequestUri = new Uri($"http://localhost/{TestInput}/behaviorsample.1.0.0.md");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                if (req.RequestUri.AbsolutePath.EndsWith(".snupkg", StringComparison.Ordinal))
                {
                    var newReq = Clone(req);
                    newReq.Headers.TryAddWithoutValidation("Original", req.RequestUri.AbsoluteUri);
                    newReq.RequestUri = new Uri($"http://localhost/{TestInput}/behaviorsample.1.0.0.snupkg.testdata");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };

            // Act
            await RunAllDriversAsync(
                driversUnderTest,
                async () =>
                {
                    await SetCursorsAsync(CatalogScanDriverMetadata.StartableDriverTypes, bucketRangeSecond ? max1 : min0);
                },
                async driverType =>
                {
                    if (bucketRangeSecond)
                    {
                        return await CatalogScanService.UpdateAsync(driverType, buckets);
                    }
                    else
                    {
                        return await CatalogScanService.UpdateAsync(driverType, max1);
                    }
                });

            // Assert
            foreach (var recordType in recordTypes)
            {
                var containerName = CsvRecordContainers.GetContainerName(recordType);
                var tableName = CsvRecordContainers.GetDefaultKustoTableName(recordType);
                await AssertCsvAsync(recordType, containerName, dir, expectCached ? Step1 : Step2, bucket, $"{tableName}.csv");
            }

            if (!appendCsvError)
            {
                Assert.Equal(expectCached ? 1 : 2, HttpMessageHandlerFactory.RequestAndResponses.Count(x => x.OriginalRequest.Method == HttpMethod.Head && x.OriginalRequest.RequestUri.AbsolutePath.EndsWith(".snupkg", StringComparison.Ordinal) && x.Response.StatusCode == HttpStatusCode.OK));
                Assert.Equal(expectCached ? 1 : 2, HttpMessageHandlerFactory.RequestAndResponses.Count(x => x.OriginalRequest.Method == HttpMethod.Get && x.OriginalRequest.RequestUri.AbsolutePath.EndsWith(".snupkg", StringComparison.Ordinal) && x.Response.StatusCode == HttpStatusCode.OK));
                Assert.Equal(expectCached ? 1 : 2, HttpMessageHandlerFactory.RequestAndResponses.Count(x => x.OriginalRequest.RequestUri.AbsolutePath.EndsWith("/readme", StringComparison.Ordinal) && x.Response.StatusCode == HttpStatusCode.OK));
            }
        }

        /// <summary>
        /// This is the catalog commit immediately preceding <see cref="Max1"/>.
        /// </summary>
        private static readonly DateTimeOffset Min0 = DateTimeOffset.Parse("2019-07-21T23:50:40.3963089Z", CultureInfo.InvariantCulture);

        /// <summary>
        /// This catalog commit has a single package that is tiny. This reduces the amount of IO for the test.
        /// </summary>
        private static readonly DateTimeOffset Max1 = DateTimeOffset.Parse("2019-07-21T23:52:24.7439835Z", CultureInfo.InvariantCulture);

        private async Task<Dictionary<CatalogScanDriverType, List<CatalogIndexScan>>> RunAllDriversAsync(
            IReadOnlySet<CatalogScanDriverType> driversUnderTest,
            Func<Task> initalizeAsync,
            Func<CatalogScanDriverType, Task<CatalogScanServiceResult>> startDriverAsync)
        {
            // Arrange
            var driversAndDependencies = driversUnderTest
                .SelectMany(CatalogScanDriverMetadata.GetTransitiveClosure)
                .Distinct()
                .Order()
                .ToHashSet();
            var batches = CatalogScanDriverMetadata.GetParallelBatches(
                driversAndDependencies,
                FrozenSet<CatalogScanDriverType>.Empty);

            Output.WriteHorizontalRule();
            Output.WriteLine("Drivers under test: " + string.Join(", ", driversUnderTest.Order()));
            var extraDependencies = driversAndDependencies.Except(driversUnderTest).Order().ToList();
            if (extraDependencies.Count > 0)
            {
                Output.WriteLine("Extra dependencies: " + string.Join(", ", extraDependencies));
            }
            foreach (var batch in batches)
            {
                Output.WriteLine("Batch: " + string.Join(", ", batch));
            }
            Output.WriteHorizontalRule();

            await initalizeAsync();

            // Act
            foreach (var batch in batches)
            {
                Output.WriteHorizontalRule();
                Output.WriteLine("Starting batch: " + string.Join(", ", batch));
                Output.WriteHorizontalRule();

                var startedScans = new List<CatalogIndexScan>();
                foreach (var driverType in batch)
                {
                    var result = await startDriverAsync(driverType);
                    Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
                    startedScans.Add(result.Scan);
                }

                foreach (var scan in startedScans)
                {
                    await UpdateAsync(scan, parallel: false, visibilityTimeout: TimeSpan.FromSeconds(1));
                }
            }

            // Assert
            Assert.NotEmpty(driversUnderTest);
            var scans = await CatalogScanStorageService.GetAllLatestIndexScansAsync(maxEntities: 5);
            Assert.All(driversAndDependencies, x => Assert.Contains(x, scans.Keys));
            Assert.All(driversAndDependencies, x => Assert.Equal(CatalogIndexScanState.Complete, Assert.Single(scans[x]).State));
            return scans;
        }

        public CatalogScanServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}

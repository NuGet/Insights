// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageAssetToCsv
{
    public class PackageAssetToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageAsset>
    {
        private const string PackageAssetToCsvDir = nameof(PackageAssetToCsv);
        private const string PackageAssetToCsv_WithSingleBucketDir = nameof(PackageAssetToCsv_WithSingleBucket);
        private const string PackageAssetToCsv_WithDeleteDir = nameof(PackageAssetToCsv_WithDelete);
        private const string PackageAssetToCsv_WithDuplicatesDir = nameof(PackageAssetToCsv_WithDuplicates);

        [Fact]
        public async Task PackageAssetToCsv()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssetToCsvDir, Step1, 0);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageAssetToCsvDir, Step2, 0);
        }

        [Fact]
        public async Task PackageAssetToCsv_WithSingleBucket()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 3; // all of the packages are in bucket 0
            var min0 = DateTimeOffset.Parse("2018-01-24T15:00:44.3794495Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2018-01-24T15:04:49.2801912Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssetToCsv_WithSingleBucketDir, Step1, 0);
            await AssertCsvCountAsync(1);

            // 3 batches of 5 leaf scans each
            Assert.True(TelemetryClient.Metrics.TryGetValue(new("TableClient.EntityChangeCount", "TableName", "OperationName"), out var entityChangeCountMetric));
            var leafScanOperations = entityChangeCountMetric.MetricValues.Where(x => x.DimensionValues[0].StartsWith(Options.Value.CatalogLeafScanTableName, StringComparison.Ordinal)).ToList();
            Assert.Equal(3, leafScanOperations.Count);
            Assert.All(leafScanOperations, x => Assert.Equal(5, x.MetricValue)); // 5 leaf scans
            Assert.All(leafScanOperations, x => Assert.Equal("SubmitTransactionAsync", x.DimensionValues[1]));

            // 15 individual entity operations (5 per batch)
            Assert.True(TelemetryClient.Metrics.TryGetValue(new("TableClient.BatchActionTypeCount", "TableName", "ActionType"), out var batchActionTypeCountMetric));
            var leafScanBatchActions = batchActionTypeCountMetric.MetricValues.Where(x => x.DimensionValues[0].StartsWith(Options.Value.CatalogLeafScanTableName, StringComparison.Ordinal)).ToList();
            Assert.Equal(15, leafScanBatchActions.Count);
            Assert.Equal(5, leafScanBatchActions.Where(x => x.DimensionValues[1] == "Add").Count());
            Assert.Equal(5, leafScanBatchActions.Where(x => x.DimensionValues[1] == "UpdateReplace").Count());
            Assert.Equal(5, leafScanBatchActions.Where(x => x.DimensionValues[1] == "Delete").Count());

            // 1 append of CSV records to table
            Assert.True(TelemetryClient.Metrics.TryGetValue(new("AppendResultStorageService.AppendToTableAsync.RecordCount", "RecordType"), out var appendToTableRecordCountMetric));
            var appendValue = Assert.Single(appendToTableRecordCountMetric.MetricValues);
            Assert.Equal(10, appendValue.MetricValue);
            Assert.Equal(nameof(PackageAsset), appendValue.DimensionValues[0]);

            // 1 bucket of CSV records
            Assert.True(TelemetryClient.Metrics.TryGetValue(new("AppendResultStorageService.AppendToTableAsync.BucketsInBatch", "RecordType"), out var bucketsInBatchMetric));
            var bucketsInBatchValue = Assert.Single(bucketsInBatchMetric.MetricValues);
            Assert.Equal(1, bucketsInBatchValue.MetricValue);
            Assert.Equal(nameof(PackageAsset), bucketsInBatchValue.DimensionValues[0]);

            // 1 catalog leaf scan batch
            Assert.True(TelemetryClient.Metrics.TryGetValue(new(MetricNames.MessageProcessedCount, "Status", "SchemaName", "IsBatch"), out var messageProcessedMetric));
            var catalogLeafScanMessageValue = Assert.Single(messageProcessedMetric.MetricValues.Where(x => x.DimensionValues[1] == "cls"));
            Assert.Equal(5, catalogLeafScanMessageValue.MetricValue);
            Assert.Equal("true", catalogLeafScanMessageValue.DimensionValues[2]);

            // 1 compacted blob
            Assert.True(TelemetryClient.Metrics.TryGetValue(new("AppendResultStorageService.CompactAsync.RecordCount", "DestContainer", "RecordType"), out var compactMetric));
            var compactValue = Assert.Single(compactMetric.MetricValues);
            Assert.Equal(10, compactValue.MetricValue);
            Assert.Equal(Options.Value.PackageAssetContainerName, compactValue.DimensionValues[0]);
            Assert.Equal(nameof(PackageAsset), compactValue.DimensionValues[1]);
        }

        [Fact]
        public async Task PackageAssetToCsv_WithoutBatching()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AllowBatching = false;

            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssetToCsvDir, Step1, 0);
        }

        [Fact]
        public async Task PackageAssetToCsv_WithBigModeAppendService()
        {
            // Arrange
            ConfigureWorkerSettings = x =>
            {
                x.AppendResultBigModeRecordThreshold = 0;
            };

            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssetToCsvDir, Step1, 0);
            TelemetryClient.Metrics.TryGetValue(new("AppendResultStorageService.CompactAsync.BigMode.Switch", "DestContainer", "RecordType", "Reason"), out var metric);
            Assert.NotNull(metric);
            Assert.All(metric.MetricValues, x => Assert.Equal(Options.Value.PackageAssetContainerName, x.DimensionValues[0]));
            Assert.All(metric.MetricValues, x => Assert.Equal("PackageAsset", x.DimensionValues[1]));
            Assert.All(metric.MetricValues, x => Assert.Equal("EstimatedRecordCount", x.DimensionValues[2]));
            TelemetryClient.Metrics.Clear();

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageAssetToCsvDir, Step2, 0);
            TelemetryClient.Metrics.TryGetValue(new("AppendResultStorageService.CompactAsync.BigMode.Switch", "DestContainer", "RecordType", "Reason"), out metric);
            Assert.NotNull(metric);
            Assert.All(metric.MetricValues, x => Assert.Equal(Options.Value.PackageAssetContainerName, x.DimensionValues[0]));
            Assert.All(metric.MetricValues, x => Assert.Equal("PackageAsset", x.DimensionValues[1]));
            Assert.All(metric.MetricValues, x => Assert.Equal("ExistingRecordCount", x.DimensionValues[2]));
        }

        [Theory]
        [InlineData("00:01:00", true, false)]
        [InlineData("00:01:00", false, false)]
        [InlineData("00:02:00", true, true)]
        [InlineData("00:02:00", false, true)]
        public async Task PackageAssetToCsv_LeafLevelTelemetry(string threshold, bool onlyLatestLeaves, bool expectLogs)
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2016-07-28T16:12:06.0020479Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2016-07-28T16:13:37.3231638Z", CultureInfo.InvariantCulture);

            ConfigureWorkerSettings = x =>
            {
                x.AllowBatching = false;
                x.LeafLevelTelemetryThreshold = TimeSpan.Parse(threshold, CultureInfo.InvariantCulture);
            };

            if (!onlyLatestLeaves)
            {
                MutableLatestLeavesTypes.Clear();
            }

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(DriverType, onlyLatestLeaves, max1);

            // Assert
            if (expectLogs)
            {
                Assert.Contains(TelemetryClient.MetricValues, x => x.MetricId == "CatalogScanExpandService.EnqueueLeafScansAsync.CatalogLeafScan");
                Assert.Contains(TelemetryClient.MetricValues, x => x.MetricId == "CatalogLeafScanMessageProcessor.ToProcess.CatalogLeafScan");
                Assert.Contains(TelemetryClient.MetricValues, x => x.MetricId == "CatalogScanStorageService.DeleteAsync.Single.CatalogLeafScan");
            }
            else
            {
                Assert.DoesNotContain(TelemetryClient.MetricValues, x => x.MetricId.EndsWith(".CatalogLeafScan", StringComparison.Ordinal));
                Assert.DoesNotContain(TelemetryClient.Metrics, x => x.Key.MetricId.EndsWith(".CatalogLeafScan", StringComparison.Ordinal));
            }
        }

        [Fact]
        public async Task PackageAssetToCsv_WithDelete()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssetToCsv_WithDeleteDir, Step1, 0);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageAssetToCsv_WithDeleteDir, Step2, 0);
        }

        [Fact]
        public Task PackageAssetToCsv_WithDuplicates_OnlyLatestLeaves()
        {
            return PackageAssetToCsv_WithDuplicates();
        }

        [Fact]
        public async Task PackageAssetToCsv_WithDuplicates_AllLeaves()
        {
            MutableLatestLeavesTypes.Clear();
            await PackageAssetToCsv_WithDuplicates();
        }

        [Fact]
        public Task PackageAssetToCsv_WithDuplicates_OnlyLatestLeaves_FailedRangeRequests()
        {
            FailRangeRequests();
            return PackageAssetToCsv_WithDuplicates();
        }

        [Fact]
        public async Task PackageAssetToCsv_WithDuplicates_AllLeaves_FailedRangeRequests()
        {
            MutableLatestLeavesTypes.Clear();
            FailRangeRequests();
            await PackageAssetToCsv_WithDuplicates();
        }

        public PackageAssetToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
            MutableLatestLeavesTypes.Add(DriverType);
        }

        protected override string DestinationContainerName => Options.Value.PackageAssetContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageAssetToCsv;

        private List<CatalogScanDriverType> MutableLatestLeavesTypes { get; } = new();

        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => MutableLatestLeavesTypes;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        private async Task PackageAssetToCsv_WithDuplicates()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssetToCsv_WithDuplicatesDir, Step1, 0);

            var duplicatePackageRequests = HttpMessageHandlerFactory
                .SuccessRequests
                .Where(x => x.RequestUri.GetLeftPart(UriPartial.Path).EndsWith("/gosms.ge-sms-api.1.0.1.nupkg", StringComparison.Ordinal))
                .ToList();
            var onlyLatestLeaves = LatestLeavesTypes.Contains(DriverType);
            Assert.Equal(onlyLatestLeaves ? 1 : 2, duplicatePackageRequests.Count(x => x.Method == HttpMethod.Get));
        }

        private void FailRangeRequests()
        {
            HttpMessageHandlerFactory.OnSendAsync = (req, b, t) =>
            {
                if (req.Method == HttpMethod.Get && req.Headers.Range is not null)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        RequestMessage = req,
                    });
                }

                return Task.FromResult<HttpResponseMessage>(null);
            };
        }

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[] { "CatalogScan-" + CatalogScanDriverType.LoadPackageArchive });
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageArchiveTableName });
        }
    }
}

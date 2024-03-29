// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.CatalogDataToCsv;

namespace NuGet.Insights.Worker
{
    public class AppendResultStorageServiceTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public async Task SetsNullStringAsEmptyOnRecord()
        {
            // Arrange
            await Target.InitializeAsync(SrcTable, DestContainer);

            var record = new PackageDeprecationRecord { Id = "Newtonsoft.Json", Version = "9.0.1", ResultType = PackageDeprecationResultType.NotDeprecated };
            record.Message = null;

            RecordsA = new CsvRecordSets<PackageDeprecationRecord>(new CsvRecordSet<PackageDeprecationRecord>(
                string.Empty,
                new[] { record }))[0];

            // Act
            await Target.AppendAsync(SrcTable, BucketCount, RecordsA);

            // Assert
            Assert.Same(string.Empty, record.Message);
        }

        [Fact]
        public async Task TracksBlobChangeForNewBlob()
        {
            // Arrange
            await Target.InitializeAsync(SrcTable, DestContainer);

            await Target.AppendAsync(SrcTable, BucketCount, RecordsA);
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);

            // Act
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket, force: false, PackageRecord.Prune);

            // Assert
            var metric = TelemetryClient.Metrics[new("AppendResultStorageService.CompactAsync.BlobChange", "DestContainer", "RecordType")];
            var value = Assert.Single(metric.MetricValues);
            Assert.Equal(1, value.MetricValue);
            Assert.Equal<string[]>([DestContainer, typeof(PackageDeprecationRecord).FullName], value.DimensionValues);
        }

        [Fact]
        public async Task TracksBlobChangeForChangedBlob()
        {
            // Arrange
            await Target.InitializeAsync(SrcTable, DestContainer);

            await Target.AppendAsync(SrcTable, BucketCount, RecordsA);
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket, force: false, PackageRecord.Prune);
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);

            // Act
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket, force: false, PackageRecord.Prune);

            // Assert
            var metric = TelemetryClient.Metrics[new("AppendResultStorageService.CompactAsync.BlobChange", "DestContainer", "RecordType")];
            Assert.Equal(2, metric.MetricValues.Count);
            var values = metric.MetricValues.ToList();
            Assert.Equal(1, values[0].MetricValue);
            Assert.Equal(1, values[1].MetricValue);
        }

        [Fact]
        public async Task TracksNoBlobChangeForNoExtraRecords()
        {
            // Arrange
            await Target.InitializeAsync(SrcTable, DestContainer);

            await Target.AppendAsync(SrcTable, BucketCount, RecordsA);
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket, force: false, PackageRecord.Prune);

            // Act
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket, force: false, PackageRecord.Prune);

            // Assert
            var metric = TelemetryClient.Metrics[new("AppendResultStorageService.CompactAsync.BlobChange", "DestContainer", "RecordType")];
            Assert.Equal(2, metric.MetricValues.Count);
            var values = metric.MetricValues.ToList();
            Assert.Equal(1, values[0].MetricValue);
            Assert.Equal(0, values[1].MetricValue);
        }

        [Fact]
        public async Task TracksNoBlobChangeForExtraDuplicateRecords()
        {
            // Arrange
            await Target.InitializeAsync(SrcTable, DestContainer);

            await Target.AppendAsync(SrcTable, BucketCount, RecordsA);
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket, force: false, PackageRecord.Prune);
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);

            // Act
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket, force: false, PackageRecord.Prune);

            // Assert
            var metric = TelemetryClient.Metrics[new("AppendResultStorageService.CompactAsync.BlobChange", "DestContainer", "RecordType")];
            Assert.Equal(2, metric.MetricValues.Count);
            var values = metric.MetricValues.ToList();
            Assert.Equal(1, values[0].MetricValue);
            Assert.Equal(0, values[1].MetricValue);
        }

        public AppendResultStorageService Target => Host.Services.GetRequiredService<AppendResultStorageService>();

        public string SrcTable { get; }
        public string DestContainer { get; }
        public int BucketCount { get; }
        public int Bucket { get; }
        public IReadOnlyList<ICsvRecordSet<ICsvRecord>> RecordsA { get; set; }
        public IReadOnlyList<ICsvRecordSet<ICsvRecord>> RecordsB { get; set; }

        public AppendResultStorageServiceTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            SrcTable = StoragePrefix + "1src1";
            DestContainer = StoragePrefix + "1dest1";
            BucketCount = 1;
            Bucket = 0;

            RecordsA = new CsvRecordSets<PackageDeprecationRecord>(new CsvRecordSet<PackageDeprecationRecord>(
                string.Empty,
                new[] { new PackageDeprecationRecord { Id = "Newtonsoft.Json", Version = "9.0.1", ResultType = PackageDeprecationResultType.NotDeprecated } }))[0];
            RecordsB = new CsvRecordSets<PackageDeprecationRecord>(new CsvRecordSet<PackageDeprecationRecord>(
                string.Empty,
                new[] { new PackageDeprecationRecord { Id = "Newtonsoft.Json", Version = "10.0.1", ResultType = PackageDeprecationResultType.NotDeprecated } }))[0];
        }
    }
}

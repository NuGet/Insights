// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO.Compression;
using System.IO.Hashing;
using Azure.Storage.Blobs.Models;
using NuGet.Insights.Worker.CatalogDataToCsv;

namespace NuGet.Insights.Worker
{
    public class AppendResultStorageServiceTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public async Task SetsNullStringAsEmptyOnRecord()
        {
            // Arrange
            await Target.InitializeAsync(SrcTable);

            var record = new PackageDeprecationRecord { Id = "Newtonsoft.Json", Version = "9.0.1", ResultType = PackageDeprecationResultType.NotDeprecated }.InitializeFromIdVersion();
            record.Message = null;

            RecordsA = [record];

            // Act
            await Target.AppendAsync(SrcTable, BucketCount, RecordsA);

            // Assert
            Assert.Same(string.Empty, record.Message);
        }

        [Fact]
        public async Task TracksBlobChangeForNewBlob()
        {
            // Arrange
            await Target.InitializeAsync(SrcTable);
            await CreateDestContainerAsync();

            await Target.AppendAsync(SrcTable, BucketCount, RecordsA);
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);

            // Act
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket);

            // Assert
            var metric = TelemetryClient.Metrics[new("CsvRecordStorageService.CompactAsync.BlobChange", "DestContainer", "RecordType", "Bucket")];
            var value = Assert.Single(metric.MetricValues);
            Assert.Equal(1, value.MetricValue);
            Assert.Equal<string[]>([DestContainer, typeof(PackageDeprecationRecord).Name, "0"], value.DimensionValues);
        }

        [Fact]
        public async Task TracksSwitchToBigMode()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultBigModeRecordThreshold = 0;
            await Target.InitializeAsync(SrcTable);
            await CreateDestContainerAsync();

            await Target.AppendAsync(SrcTable, BucketCount, RecordsA);
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);

            // Act
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket);

            // Assert
            var metric = TelemetryClient.Metrics[new("CsvRecordStorageService.CompactAsync.BigMode.Switch", "DestContainer", "RecordType", "Bucket", "Reason")];
            var value = Assert.Single(metric.MetricValues);
            Assert.Equal(1, value.MetricValue);
            Assert.Equal<string[]>([DestContainer, typeof(PackageDeprecationRecord).Name, "0", "EstimatedRecordCount"], value.DimensionValues);
        }

        [Fact]
        public async Task TracksBlobChangeForChangedBlob()
        {
            // Arrange
            await Target.InitializeAsync(SrcTable);
            await CreateDestContainerAsync();

            await Target.AppendAsync(SrcTable, BucketCount, RecordsA);
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket);
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);

            // Act
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket);

            // Assert
            var metric = TelemetryClient.Metrics[new("CsvRecordStorageService.CompactAsync.BlobChange", "DestContainer", "RecordType", "Bucket")];
            Assert.Equal(2, metric.MetricValues.Count);
            var values = metric.MetricValues.ToList();
            Assert.Equal(1, values[0].MetricValue);
            Assert.Equal(1, values[1].MetricValue);
        }

        [Fact]
        public async Task TracksNoBlobChangeForNoExtraRecords()
        {
            // Arrange
            await Target.InitializeAsync(SrcTable);
            await CreateDestContainerAsync();

            await Target.AppendAsync(SrcTable, BucketCount, RecordsA);
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket);

            // Act
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket);

            // Assert
            var metric = TelemetryClient.Metrics[new("CsvRecordStorageService.CompactAsync.BlobChange", "DestContainer", "RecordType", "Bucket")];
            Assert.Equal(2, metric.MetricValues.Count);
            var values = metric.MetricValues.ToList();
            Assert.Equal(1, values[0].MetricValue);
            Assert.Equal(0, values[1].MetricValue);
        }

        [Fact]
        public async Task TracksNoBlobChangeForExtraDuplicateRecords()
        {
            // Arrange
            await Target.InitializeAsync(SrcTable);
            await CreateDestContainerAsync();

            await Target.AppendAsync(SrcTable, BucketCount, RecordsA);
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket);
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);

            // Act
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket);

            // Assert
            var metric = TelemetryClient.Metrics[new("CsvRecordStorageService.CompactAsync.BlobChange", "DestContainer", "RecordType", "Bucket")];
            Assert.Equal(2, metric.MetricValues.Count);
            var values = metric.MetricValues.ToList();
            Assert.Equal(1, values[0].MetricValue);
            Assert.Equal(0, values[1].MetricValue);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ProducesValidGzipFile(bool bigMode)
        {
            // Arrange
            var random = new Random(Seed: 0);
            ConfigureWorkerSettings = x => x.AppendResultBigModeRecordThreshold = bigMode ? 0 : int.MaxValue;
            await Target.InitializeAsync(SrcTable);
            await CreateDestContainerAsync();

            // Step 1: append two sets of records
            // Act
            await Target.AppendAsync(SrcTable, BucketCount, RecordsA);
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket);

            // Assert
            await ValidateGzippedFormatAsync(DestContainer, Bucket);

            // Step 2: duplicate records
            // Act
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket);

            // Assert
            await ValidateGzippedFormatAsync(DestContainer, Bucket);

            // Step 3: larger records
            // Act
            foreach (var record in RecordsA.Concat(RecordsB))
            {
                record.ScanId = new Guid("fbdc63ed-d515-487a-a0d1-0c75fe594493");
                record.ScanTimestamp = new DateTimeOffset(2024, 8, 30, 11, 23, 0, TimeSpan.Zero);
                var buffer = new byte[5000];
                random.NextBytes(buffer);
                record.Message = buffer.ToBase64();
            }

            await Target.AppendAsync(SrcTable, BucketCount, RecordsA);
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket);

            // Assert
            await ValidateGzippedFormatAsync(DestContainer, Bucket);

            // Step 4: smaller records
            // Act
            foreach (var record in RecordsA.Concat(RecordsB))
            {
                record.ScanId = new Guid("95883cea-169f-40c5-b72c-ae053531a7e5");
                record.ScanTimestamp = new DateTimeOffset(2024, 8, 31, 11, 23, 0, TimeSpan.Zero);
                var buffer = new byte[10];
                random.NextBytes(buffer);
                record.Message = buffer.ToBase64();
            }

            await Target.AppendAsync(SrcTable, BucketCount, RecordsA);
            await Target.AppendAsync(SrcTable, BucketCount, RecordsB);
            await Target.CompactAsync<PackageDeprecationRecord>(SrcTable, DestContainer, Bucket);

            // Assert
            await ValidateGzippedFormatAsync(DestContainer, Bucket);
        }

        public AppendResultStorageService Target => Host.Services.GetRequiredService<AppendResultStorageService>();
        public CsvRecordStorageService StorageService => Host.Services.GetRequiredService<CsvRecordStorageService>();

        public string SrcTable { get; }
        public string DestContainer { get; }
        public int BucketCount { get; }
        public int Bucket { get; }
        public IReadOnlyList<PackageDeprecationRecord> RecordsA { get; set; }
        public IReadOnlyList<PackageDeprecationRecord> RecordsB { get; set; }

        public AppendResultStorageServiceTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            SrcTable = StoragePrefix + "1src1";
            DestContainer = StoragePrefix + "1dest1";
            BucketCount = 1;
            Bucket = 0;

            RecordsA = [new PackageDeprecationRecord { Id = "Newtonsoft.Json", Version = "9.0.1", ResultType = PackageDeprecationResultType.NotDeprecated }.InitializeFromIdVersion()];
            RecordsB = [new PackageDeprecationRecord { Id = "Newtonsoft.Json", Version = "10.0.1", ResultType = PackageDeprecationResultType.NotDeprecated }.InitializeFromIdVersion()];
        }

        private async Task ValidateGzippedFormatAsync(string destContainer, int bucket)
        {
            var client = await StorageService.GetCompactBlobClientAsync(destContainer, bucket);
            BlobDownloadResult result = await client.DownloadContentAsync();

            var compressedBytes = result.Content.ToArray();
            var decompressedSystem = DecompressGzipUsingSystem(compressedBytes);
            var decompressedSharpCompress = DecompressGzipUsingSharpCompress(compressedBytes);

            Assert.Equal(decompressedSystem, decompressedSharpCompress);
        }

        private byte[] DecompressGzipUsingSharpCompress(byte[] compressedBytes)
        {
            using var compressedStream = new MemoryStream(compressedBytes);
            using var decompressStream = new MemoryStream();

            // We need to use a more strict gzip implementation.
            // The System.IO.Compression implementation is not strict.
            // See: https://github.com/dotnet/runtime/issues/47563
            // This was fixed, but then reverted in the .NET 7 timeframe.
            // See: https://github.com/dotnet/runtime/issues/72726
            using var gzipStream = new SharpCompress.Compressors.Deflate.GZipStream(compressedStream, SharpCompress.Compressors.CompressionMode.Decompress);
            gzipStream.CopyTo(decompressStream);
            var crc32 = gzipStream.Crc32;
            gzipStream.Dispose();

            var decompressed = decompressStream.ToArray();
            Assert.Equal(BitConverter.ToInt32(Crc32.Hash(decompressed)), gzipStream.Crc32);

            return decompressed;
        }

        private byte[] DecompressGzipUsingSystem(byte[] compressedBytes)
        {
            using var compressedStream = new MemoryStream(compressedBytes);
            using var decompressStream = new MemoryStream();

            // We need to use a more strict gzip implementation.
            using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            gzipStream.CopyTo(decompressStream);
            gzipStream.Dispose();

            return decompressStream.ToArray();
        }

        private async Task CreateDestContainerAsync()
        {
            await (await ServiceClientFactory.GetBlobServiceClientAsync()).GetBlobContainerClient(DestContainer).CreateIfNotExistsAsync(retry: true);
        }
    }
}

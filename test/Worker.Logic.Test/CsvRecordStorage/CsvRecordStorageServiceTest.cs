// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Blobs.Models;
using NuGet.Insights.Worker.DownloadsToCsv;

namespace NuGet.Insights.Worker
{
    public class CsvRecordStorageServiceTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public async Task LimitsSubdivisionsTo50()
        {
            // Arrange
            ConfigureWorkerSettings = x =>
            {
                x.AppendResultBigModeRecordThreshold = 199;
                x.AppendResultBigModeSubdivisionSize = 1;
            };
            Records.AddRange(Enumerable
                .Range(0, 200)
                .Select(x => new PackageDownloadRecord { LowerId = $"package{x}", Identity = $"package{x}/1.0.0", Id = $"Package{x}", Version = "1.0.0" }));
            var container = StoragePrefix + "dest";
            await Target.InitializeAsync(container);

            // Act
            await Target.CompactAsync(RecordProvider.Object, container, 0);

            // Assert
            TelemetryClient.Metrics.TryGetValue(new("CsvRecordStorageService.CompactAsync.BigMode.Subdivisions", "DestContainer", "RecordType"), out var metric);
            Assert.NotNull(metric);
            Assert.Equal(50, Assert.Single(metric.MetricValues).MetricValue);
        }

        public CsvRecordStorageService Target => Host.Services.GetRequiredService<CsvRecordStorageService>();

        public List<PackageDownloadRecord> Records { get; }
        public Mock<ICsvRecordProvider<PackageDownloadRecord>> RecordProvider { get; }

        public CsvRecordStorageServiceTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            Records = new List<PackageDownloadRecord>();
            RecordProvider = new Mock<ICsvRecordProvider<PackageDownloadRecord>>();

            RecordProvider
                .Setup(x => x.ShouldCompact(It.IsAny<BlobProperties>(), It.IsAny<ILogger>()))
                .Returns(true);
            RecordProvider
                .Setup(x => x.GetChunksAsync(It.IsAny<int>()))
                .Returns(() => Records.Select(r => Wrap(r)).ToAsyncEnumerable());
            RecordProvider
                .Setup(x => x.Prune(It.IsAny<List<PackageDownloadRecord>>(), It.IsAny<bool>(), It.IsAny<IOptions<NuGetInsightsWorkerSettings>>(), It.IsAny<ILogger>()))
                .Returns<List<PackageDownloadRecord>, bool, IOptions<NuGetInsightsWorkerSettings>, ILogger>((records, _, _, _) => records);
        }

        private ICsvRecordChunk<T> Wrap<T>(T record) where T : ICsvRecord<T>
        {
            var mock = new Mock<ICsvRecordChunk<T>>();
            mock.Setup(x => x.Position).Returns(string.Empty);
            mock.Setup(x => x.GetRecords()).Returns([record]);
            return mock.Object;
        }
    }
}

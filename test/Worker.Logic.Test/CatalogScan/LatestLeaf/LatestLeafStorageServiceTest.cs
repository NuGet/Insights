// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class LatestLeafStorageServiceTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public async Task MergesMixOfNewAndOldData()
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: 0, lastId: 2, commitDay: 5, instanceLabel: "a"), Storage);
            await Target.AddAsync(GetItems(firstId: 3, lastId: 5, commitDay: 1, instanceLabel: "a"), Storage);
            await Target.AddAsync(GetItems(firstId: 6, lastId: 8, commitDay: 3, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 1, lastId: 7, commitDay: 4, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(new[] { "a", "a", "a", "b", "b", "b", "b", "b", "a" }, instanceLabels);
        }

        [Fact]
        public async Task KeepsExistingDataIfCommitTimestampIsOlder()
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: 0, lastId: 2, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 2, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("a", 3), instanceLabels);
        }

        [Fact]
        public async Task KeepsExistingDataIfCommitTimestampMatches()
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: 0, lastId: 2, commitDay: 1, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 2, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("a", 3), instanceLabels);
        }

        [Fact]
        public async Task TakesIncomingDataIfCommitTimestampIsNewer()
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: 0, lastId: 2, commitDay: 1, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 2, commitDay: 2, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", 3), instanceLabels);
        }

        [Fact]
        public async Task FirstAfterBatchIsConflict()
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: 100, lastId: 100, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 102, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", 100).Concat(["a", "b", "b"]), instanceLabels);
        }

        [Fact]
        public async Task SecondAfterBatchIsConflict()
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: 101, lastId: 101, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 102, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", 100).Concat(["b", "a", "b"]), instanceLabels);
        }

        [Fact]
        public async Task ThirdAfterBatchIsConflict()
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: 102, lastId: 102, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 102, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", 100).Concat(["b", "b", "a"]), instanceLabels);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public async Task NthLeafIsConflict(int id)
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: id, lastId: id, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 6, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", id).Append("a").Concat(Enumerable.Repeat("b", 6 - id)), instanceLabels);
        }

        [Fact]
        public async Task ThirdAndFifthAreConflict()
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: 2, lastId: 2, commitDay: 2, instanceLabel: "a"), Storage);
            await Target.AddAsync(GetItems(firstId: 4, lastId: 4, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 6, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(new[] { "b", "b", "a", "b", "a", "b", "b" }, instanceLabels);
        }

        [Fact]
        public async Task LotsOfOldDuplicatesLessThanOnePage()
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: 0, lastId: 59, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 59, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("a", 60), instanceLabels);
        }

        [Fact]
        public async Task LotsOfNewDuplicatesLessThanOnePage()
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: 0, lastId: 59, commitDay: 1, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 59, commitDay: 2, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", 60), instanceLabels);
        }

        [Fact]
        public async Task LotsOfOldDuplicatesLessMoreOnePage()
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: 0, lastId: 3199, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 3199, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("a", 3200), instanceLabels);
        }

        [Fact]
        public async Task LotsOfNewDuplicatesMoreThanOnePage()
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: 0, lastId: 3199, commitDay: 1, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 3199, commitDay: 2, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", 3200), instanceLabels);
        }

        [Fact]
        public async Task OldInterleavedDuplicatesMoreThanOnePage()
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: 1, lastId: 3199, commitDay: 1, instanceLabel: "a", skipBy: 2), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 3199, commitDay: 2, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", 3200), instanceLabels);
        }

        [Fact]
        public async Task NewInterleavedDuplicatesMoreThanOnePage()
        {
            // Arrange
            await Target.AddAsync(GetItems(firstId: 1, lastId: 3199, commitDay: 2, instanceLabel: "a", skipBy: 2), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 3199, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat(new[] { "b", "a" }, 1600).SelectMany(x => x).ToArray(), instanceLabels);
        }

        public LatestLeafStorageService<CatalogLeafScan> Target => Host.Services.GetRequiredService<LatestLeafStorageService<CatalogLeafScan>>();
        public ILatestPackageLeafStorageFactory<CatalogLeafScan> StorageFactory => Host.Services.GetRequiredService<ILatestPackageLeafStorageFactory<CatalogLeafScan>>();

        public CatalogIndexScan IndexScan { get; }
        public CatalogPageScan PageScan { get; }
        public IReadOnlyDictionary<ICatalogLeafItem, int> LeafItemToRank { get; }
        public ILatestPackageLeafStorage<CatalogLeafScan> Storage { get; private set; }

        public override async Task InitializeAsync()
        {
            await CatalogScanStorageService.InitializeAsync();
            await CatalogScanStorageService.InitializeLeafScanTableAsync(IndexScan.StorageSuffix);
            await CatalogScanStorageService.InsertAsync(IndexScan);
            await StorageFactory.InitializeAsync();
            Storage = await StorageFactory.CreateAsync(PageScan, LeafItemToRank);
        }

        public LatestLeafStorageServiceTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            IndexScan = new CatalogIndexScan(CatalogScanDriverType.PackageAssetToCsv, "scan-id", "suffix")
            {
                Min = StorageUtility.MinTimestamp,
                Max = StorageUtility.MinTimestamp,
            };
            PageScan = new CatalogPageScan
            {
                ParentDriverType = IndexScan.DriverType,
                ParentScanId = IndexScan.ScanId,
                Url = "https://example/page0.json",
            };
            LeafItemToRank = new Dictionary<ICatalogLeafItem, int>();

            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;
        }

        private List<CatalogLeafItem> GetItems(int firstId, int lastId, int commitDay, string instanceLabel, int skipBy = 1)
        {
            var output = new List<CatalogLeafItem>();
            var currentId = firstId;
            while (currentId <= lastId)
            {
                output.Add(new CatalogLeafItem
                {
                    PackageId = $"{currentId:D10}",
                    PackageVersion = "1.0.0",
                    CommitTimestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(commitDay),
                    Url = instanceLabel,
                });

                currentId += skipBy;
            }

            return output;
        }

        private async Task<string[]> GetInstanceLabelsAsync()
        {
            var leafScans = await CatalogScanStorageService.GetLeafScansAsync(IndexScan.StorageSuffix, IndexScan.ScanId, "B000");
            var maxId = leafScans.Max(x => int.Parse(x.PackageId, CultureInfo.InvariantCulture));
            var instanceLabels = new string[maxId + 1];
            foreach (var leafScan in leafScans)
            {
                instanceLabels[int.Parse(leafScan.PackageId, CultureInfo.InvariantCulture)] = leafScan.Url;
            }

            return instanceLabels;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class LatestLeafStorageServiceTest : BaseWorkerLogicIntegrationTest
    {
        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task HandlesSingleItem(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 0, commitDay: 1, instanceLabel: "a"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(new[] { "a" }, instanceLabels);
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task HandlesMultiplePartitionKeys(EntityUpsertStrategy strategy)
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 3;
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            var lastId = 20;
            await Target.AddAsync(GetItems(firstId: 0, lastId, commitDay: 1, instanceLabel: "b"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId, commitDay: 2, instanceLabel: "a"), Storage);

            // Assert
            var instanceLabels0 = await GetInstanceLabelsAsync(0, lastId);
            var instanceLabels1 = await GetInstanceLabelsAsync(1, lastId);
            var instanceLabels2 = await GetInstanceLabelsAsync(2, lastId);
            Assert.Equal("a aaaa     a   aa  a ", string.Join(string.Empty, instanceLabels0));
            Assert.Equal("              a     a", string.Join(string.Empty, instanceLabels1));
            Assert.Equal(" a    aaaaa aa   aa  ", string.Join(string.Empty, instanceLabels2));
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task MergesMixOfNewAndOldData(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            await Target.AddAsync(GetItems(firstId: 0, lastId: 2, commitDay: 5, instanceLabel: "a"), Storage);
            await Target.AddAsync(GetItems(firstId: 3, lastId: 5, commitDay: 1, instanceLabel: "a"), Storage);
            await Target.AddAsync(GetItems(firstId: 6, lastId: 8, commitDay: 3, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 1, lastId: 7, commitDay: 4, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(new[] { "a", "a", "a", "b", "b", "b", "b", "b", "a" }, instanceLabels);
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task KeepsExistingDataIfCommitTimestampIsOlder(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            await Target.AddAsync(GetItems(firstId: 0, lastId: 2, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 2, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("a", 3), instanceLabels);
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task KeepsExistingDataIfCommitTimestampMatches(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            await Target.AddAsync(GetItems(firstId: 0, lastId: 2, commitDay: 1, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 2, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("a", 3), instanceLabels);
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task TakesIncomingDataIfCommitTimestampIsNewer(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            await Target.AddAsync(GetItems(firstId: 0, lastId: 2, commitDay: 1, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 2, commitDay: 2, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", 3), instanceLabels);
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task FirstAfterBatchIsConflict(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            await Target.AddAsync(GetItems(firstId: 100, lastId: 100, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 102, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", 100).Concat(["a", "b", "b"]), instanceLabels);
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task SecondAfterBatchIsConflict(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            await Target.AddAsync(GetItems(firstId: 101, lastId: 101, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 102, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", 100).Concat(["b", "a", "b"]), instanceLabels);
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task ThirdAfterBatchIsConflict(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            await Target.AddAsync(GetItems(firstId: 102, lastId: 102, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 102, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", 100).Concat(["b", "b", "a"]), instanceLabels);
        }

        public static IEnumerable<object[]> StrategyNthLeafTestData =>
            from strategy in Enum.GetValues<EntityUpsertStrategy>()
            from id in Enumerable.Range(0, 7)
            select new object[] { strategy, id };

        [Theory]
        [MemberData(nameof(StrategyNthLeafTestData))]
        public async Task NthLeafIsConflict(EntityUpsertStrategy strategy, int id)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            await Target.AddAsync(GetItems(firstId: id, lastId: id, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 6, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", id).Append("a").Concat(Enumerable.Repeat("b", 6 - id)), instanceLabels);
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task ThirdAndFifthAreConflict(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            await Target.AddAsync(GetItems(firstId: 2, lastId: 2, commitDay: 2, instanceLabel: "a"), Storage);
            await Target.AddAsync(GetItems(firstId: 4, lastId: 4, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 6, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(new[] { "b", "b", "a", "b", "a", "b", "b" }, instanceLabels);
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task LotsOfOldDuplicatesLessThanOnePage(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            await Target.AddAsync(GetItems(firstId: 0, lastId: 59, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 59, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("a", 60), instanceLabels);
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task LotsOfNewDuplicatesLessThanOnePage(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            await Target.AddAsync(GetItems(firstId: 0, lastId: 59, commitDay: 1, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 59, commitDay: 2, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", 60), instanceLabels);
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task LotsOfOldDuplicatesLessMoreOnePage(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            await Target.AddAsync(GetItems(firstId: 0, lastId: 3199, commitDay: 2, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 3199, commitDay: 1, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("a", 3200), instanceLabels);
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task LotsOfNewDuplicatesMoreThanOnePage(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            await Target.AddAsync(GetItems(firstId: 0, lastId: 3199, commitDay: 1, instanceLabel: "a"), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 3199, commitDay: 2, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", 3200), instanceLabels);
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task OldInterleavedDuplicatesMoreThanOnePage(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
            await Target.AddAsync(GetItems(firstId: 1, lastId: 3199, commitDay: 1, instanceLabel: "a", skipBy: 2), Storage);

            // Act
            await Target.AddAsync(GetItems(firstId: 0, lastId: 3199, commitDay: 2, instanceLabel: "b"), Storage);

            // Assert
            var instanceLabels = await GetInstanceLabelsAsync();
            Assert.Equal(Enumerable.Repeat("b", 3200), instanceLabels);
        }

        [Theory]
        [MemberData(nameof(StrategyTestData))]
        public async Task NewInterleavedDuplicatesMoreThanOnePage(EntityUpsertStrategy strategy)
        {
            // Arrange
            await InitializeStorageAsync();
            MockStorage.Setup(x => x.Strategy).Returns(strategy);
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
        public Mock<ILatestPackageLeafStorage<CatalogLeafScan>> MockStorage { get; private set; }
        public ILatestPackageLeafStorage<CatalogLeafScan> Storage => MockStorage?.Object;

        protected async Task InitializeStorageAsync()
        {
            await CatalogScanStorageService.InitializeAsync();
            await CatalogScanStorageService.InitializeLeafScanTableAsync(IndexScan.StorageSuffix);
            await CatalogScanStorageService.InsertAsync(IndexScan);
            await StorageFactory.InitializeAsync();

            var storage = await StorageFactory.CreateAsync(PageScan, LeafItemToRank);
            MockStorage = new Mock<ILatestPackageLeafStorage<CatalogLeafScan>>();
            MockStorage.Setup(x => x.Table).Returns(() => storage.Table);
            MockStorage.Setup(x => x.CommitTimestampColumnName).Returns(() => storage.CommitTimestampColumnName);
            MockStorage.Setup(x => x.Strategy).Returns(() => storage.Strategy);

            MockStorage
                .Setup(x => x.MapAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ICatalogLeafItem>()))
                .Returns<string, string, ICatalogLeafItem>(storage.MapAsync);

            MockStorage
                .Setup(x => x.GetKey(It.IsAny<ICatalogLeafItem>()))
                .Returns<ICatalogLeafItem>(storage.GetKey);
        }

        public static IEnumerable<object[]> StrategyTestData => Enum
            .GetValues<EntityUpsertStrategy>()
            .Select(x => new object[] { x });

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

        private async Task<string[]> GetInstanceLabelsAsync(int bucket = 0, int? lastId = null)
        {
            var leafScans = await CatalogScanStorageService.GetLeafScansByPageIdAsync(IndexScan.StorageSuffix, IndexScan.ScanId, $"B{bucket:D3}");
            lastId = lastId ?? leafScans.Max(x => int.Parse(x.PackageId, CultureInfo.InvariantCulture));
            var instanceLabels = new string[lastId.Value + 1];
            Array.Fill(instanceLabels, " ");
            foreach (var leafScan in leafScans)
            {
                instanceLabels[int.Parse(leafScan.PackageId, CultureInfo.InvariantCulture)] = leafScan.Url;
            }

            return instanceLabels;
        }
    }
}

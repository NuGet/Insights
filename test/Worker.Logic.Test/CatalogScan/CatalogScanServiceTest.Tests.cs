// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;

namespace NuGet.Insights.Worker
{
    public partial class CatalogScanServiceTest : BaseWorkerLogicIntegrationTest
    {
        public class TheAbortAsyncMethod : CatalogScanServiceTest
        {
            [Fact]
            public async Task AbortCleansUpFindLatest()
            {
                // Arrange
                DriverType = CatalogScanDriverType.PackageAssetToCsv;
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(DriverType, CursorValue);
                var scanResult = await CatalogScanService.UpdateAsync(DriverType);
                var scan = scanResult.Scan;

                var processor = Host.Services.GetRequiredService<IMessageProcessor<CatalogIndexScanMessage>>();
                var message = new CatalogIndexScanMessage { DriverType = DriverType, ScanId = scan.ScanId };
                await processor.ProcessAsync(message, dequeueCount: 0);
                scan = await CatalogScanStorageService.GetIndexScanAsync(DriverType, scan.ScanId);
                var scansBefore = await CatalogScanStorageService.GetIndexScansAsync();

                // Act
                var aborted = await CatalogScanService.AbortAsync(DriverType);

                // Assert
                Assert.Equal(CatalogIndexScanState.FindingLatest, scan.State);
                Assert.Equal(scan.ScanId, aborted.ScanId);

                Assert.Equal(2, scansBefore.Count);
                Assert.Contains(scan.ScanId, scansBefore.Select(x => x.ScanId));
                Assert.Contains(CatalogScanDriverType.Internal_FindLatestCatalogLeafScan, scansBefore.Select(x => x.DriverType));

                var scansAfter = await CatalogScanStorageService.GetIndexScansAsync();
                Assert.Single(scansAfter);
                Assert.Contains(scan.ScanId, scansAfter.Select(x => x.ScanId));
                Assert.DoesNotContain(CatalogScanDriverType.Internal_FindLatestCatalogLeafScan, scansAfter.Select(x => x.DriverType));
            }

            [Fact]
            public async Task AbortCleansUpTemporaryCsvState()
            {
                // Arrange
                DriverType = CatalogScanDriverType.CatalogDataToCsv;
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(DriverType, CursorValue);
                var scanResult = await CatalogScanService.UpdateAsync(DriverType);
                var scan = scanResult.Scan;

                var processor = Host.Services.GetRequiredService<IMessageProcessor<CatalogIndexScanMessage>>();
                var message = new CatalogIndexScanMessage { DriverType = DriverType, ScanId = scan.ScanId };
                await processor.ProcessAsync(message, dequeueCount: 0);
                scan = await CatalogScanStorageService.GetIndexScanAsync(DriverType, scan.ScanId);
                var tablesBefore = await GetTableNamesAsync();

                // Act
                var aborted = await CatalogScanService.AbortAsync(DriverType);

                // Assert
                Assert.Equal(CatalogIndexScanState.Working, scan.State);
                Assert.Equal(scan.ScanId, aborted.ScanId);

                Assert.Contains(Options.Value.CursorTableName, tablesBefore);
                Assert.Contains(Options.Value.CatalogIndexScanTableName, tablesBefore);
                Assert.Contains($"{Options.Value.CatalogPageScanTableNamePrefix}{scan.StorageSuffix}", tablesBefore);
                Assert.Contains($"{Options.Value.CatalogLeafScanTableNamePrefix}{scan.StorageSuffix}", tablesBefore);
                Assert.Contains($"{Options.Value.CsvRecordTableNamePrefix}{scan.StorageSuffix}0", tablesBefore);
                Assert.Contains($"{Options.Value.CsvRecordTableNamePrefix}{scan.StorageSuffix}1", tablesBefore);
                Assert.Contains($"{Options.Value.CsvRecordTableNamePrefix}{scan.StorageSuffix}2", tablesBefore);
                Assert.Contains($"{Options.Value.TaskStateTableNamePrefix}{scan.StorageSuffix}", tablesBefore);
                Assert.Equal(8, tablesBefore.Count);

                var tablesAfter = await GetTableNamesAsync();
                Assert.Contains(Options.Value.CursorTableName, tablesAfter);
                Assert.Contains(Options.Value.CatalogIndexScanTableName, tablesAfter);
                Assert.Equal(2, tablesAfter.Count);
            }

            [Fact]
            public async Task AbortCanBeRunImmediately()
            {
                // Arrange
                DriverType = CatalogScanDriverType.CatalogDataToCsv;
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(DriverType, CursorValue);
                var scanResult = await CatalogScanService.UpdateAsync(DriverType);
                var scan = scanResult.Scan;
                var tablesBefore = await GetTableNamesAsync();

                // Act
                var aborted = await CatalogScanService.AbortAsync(DriverType);

                // Assert
                Assert.Equal(scan.ScanId, aborted.ScanId);

                Assert.Contains(Options.Value.CursorTableName, tablesBefore);
                Assert.Contains(Options.Value.CatalogIndexScanTableName, tablesBefore);
                Assert.Equal(2, tablesBefore.Count);

                var tablesAfter = await GetTableNamesAsync();
                Assert.Equal(tablesBefore, tablesAfter);
            }

            private async Task<List<string>> GetTableNamesAsync()
            {
                var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
                var tables = await tableServiceClient.QueryAsync().ToListAsync();
                return tables.Select(x => x.Name).Where(x => x.StartsWith(StoragePrefix, StringComparison.Ordinal)).ToList();
            }

            public TheAbortAsyncMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public class TheUpdateAsyncWithBucketsMethod : CatalogScanServiceTest
        {
            [Fact]
            public async Task AlreadyRunning()
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                var first = await CatalogScanService.UpdateAsync(ScanId, StorageSuffix, DriverType, Buckets);

                // Act
                var result = await CatalogScanService.UpdateAsync(ScanId, StorageSuffix, DriverType, Buckets);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.AlreadyStarted, result.Type);
                Assert.Equal(first.Scan.ScanId, result.Scan.ScanId);
                Assert.Equal(ScanId, result.Scan.ScanId);
            }

            [Fact]
            public async Task CanRunWhenAllCursorsAreInitialValues()
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(DriverType, CursorTableEntity.Min);

                // Act
                var result = await CatalogScanService.UpdateAsync(ScanId, StorageSuffix, DriverType, Buckets);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
                Assert.NotNull(result.Scan);
            }

            [Fact]
            public async Task Disabled()
            {
                // Arrange
                ConfigureWorkerSettings = x => x.DisabledDrivers.Add(DriverType);
                await CatalogScanService.InitializeAsync();

                // Act
                var result = await CatalogScanService.UpdateAsync(ScanId, StorageSuffix, DriverType, Buckets);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.Disabled, result.Type);
                Assert.Null(result.Scan);
                Assert.Null(result.DependencyName);
            }

            [Fact]
            public async Task BlockedByDependency()
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, CursorValue);

                // Act
                var result = await CatalogScanService.UpdateAsync(ScanId, StorageSuffix, DriverType, Buckets);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.BlockedByDependency, result.Type);
                Assert.Null(result.Scan);
                Assert.Equal(CatalogScanDriverType.LoadPackageArchive.ToString(), result.DependencyName);
            }

            [Theory]
            [MemberData(nameof(FailsToStartWithBucketsData))]
            public async Task FailsToStartWithBuckets(string typeName)
            {
                // Arrange
                var type = CatalogScanDriverType.Parse(typeName);

                // Arrange & Act & Assert
                var ex = await Assert.ThrowsAsync<ArgumentException>(() => CatalogScanService.UpdateAsync(
                    ScanId,
                    StorageSuffix,
                    type,
                    Buckets));
                Assert.StartsWith(
                    $"The driver {type} is not supported for bucket range processing.",
                    ex.Message,
                    StringComparison.Ordinal);
            }

            public static IEnumerable<object[]> FailsToStartWithBucketsData => TypeToInfo
                .Where(x => !x.Value.SupportsBucketRangeProcessing)
                .Select(x => new object[] { x.Key.ToString() });

            [Theory]
            [MemberData(nameof(StartsWithBucketsData))]
            public async Task StartsWithBuckets(string typeName)
            {
                // Arrange
                var type = CatalogScanDriverType.Parse(typeName);
                await CatalogScanService.InitializeAsync();
                var expectedMax = new DateTimeOffset(2023, 11, 22, 9, 37, 13, TimeSpan.Zero);
                await SetDependencyCursorsAsync(type, expectedMax);
                await SetCursorsAsync(
                    CatalogScanDriverMetadata.GetTransitiveClosure(type).Append(CatalogScanDriverType.LoadBucketedPackage),
                    expectedMax);

                // Act
                var result = await CatalogScanService.UpdateAsync(ScanId, StorageSuffix, type, Buckets);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
                Assert.Equal(type, result.Scan.DriverType);
                Assert.Equal(ScanId, result.Scan.ScanId);
                Assert.Equal("23-25,42", result.Scan.BucketRanges);
                Assert.Equal(CatalogClient.NuGetOrgMinDeleted, result.Scan.Min);
                Assert.Equal(expectedMax, result.Scan.Max);
                Assert.Equal(string.Empty, result.Scan.CursorName);
                Assert.False(result.Scan.ContinueUpdate);
                Assert.True(result.Scan.OnlyLatestLeaves);
                Assert.Null(result.Scan.ParentDriverType);
                Assert.Null(result.Scan.ParentScanId);
            }

            public static IEnumerable<object[]> StartsWithBucketsData => TypeToInfo
                .Where(x => x.Value.SupportsBucketRangeProcessing)
                .Select(x => new object[] { x.Key.ToString() });

            public string ScanId { get; }
            public string StorageSuffix { get; }
            public int[] Buckets { get; }

            public TheUpdateAsyncWithBucketsMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
                ScanId = "my-scan-id";
                StorageSuffix = "zz";
                Buckets = new[] { 23, 24, 42, 25 };
            }
        }

        public class TheUpdateAsyncMethod : CatalogScanServiceTest
        {
            [Fact]
            public async Task AlreadyRunning()
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(DriverType, CursorValue);
                var first = await CatalogScanService.UpdateAsync(DriverType);

                // Act
                var result = await CatalogScanService.UpdateAsync(DriverType);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.AlreadyStarted, result.Type);
                Assert.Equal(first.Scan.ScanId, result.Scan.ScanId);
            }

            [Fact]
            public async Task BlockedByDependencyThatHasNeverRun()
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(DriverType, CursorTableEntity.Min);

                // Act
                var result = await CatalogScanService.UpdateAsync(DriverType);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.BlockedByDependency, result.Type);
                Assert.Null(result.Scan);
                Assert.Equal(CatalogScanDriverType.LoadPackageArchive.ToString(), result.DependencyName);
            }

            [Fact]
            public async Task Disabled()
            {
                // Arrange
                ConfigureWorkerSettings = x => x.DisabledDrivers.Add(DriverType);
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(DriverType, CursorValue);

                // Act
                var result = await CatalogScanService.UpdateAsync(DriverType, CursorValue);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.Disabled, result.Type);
                Assert.Null(result.Scan);
                Assert.Null(result.DependencyName);
            }

            [Fact]
            public async Task BlockedByDependency()
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(DriverType, CursorValue);

                // Act
                var result = await CatalogScanService.UpdateAsync(DriverType, max: CursorValue.AddTicks(1));

                // Assert
                Assert.Equal(CatalogScanServiceResultType.BlockedByDependency, result.Type);
                Assert.Null(result.Scan);
                Assert.Equal(CatalogScanDriverType.LoadPackageArchive.ToString(), result.DependencyName);
            }

            [Fact]
            public async Task MinAfterMax()
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(DriverType, CursorValue);

                // Act
                var result = await CatalogScanService.UpdateAsync(DriverType, max: CursorTableEntity.Min.AddTicks(-1));

                // Assert
                Assert.Equal(CatalogScanServiceResultType.MinAfterMax, result.Type);
                Assert.Null(result.Scan);
                Assert.Null(result.DependencyName);
            }

            [Fact]
            public async Task FullyCaughtUpWithDependency()
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(DriverType, CursorValue);
                await SetCursorAsync(DriverType, CursorValue);

                // Act
                var result = await CatalogScanService.UpdateAsync(DriverType);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.FullyCaughtUpWithDependency, result.Type);
                Assert.Null(result.Scan);
                Assert.Equal(CatalogScanDriverType.LoadPackageArchive.ToString(), result.DependencyName);
            }

            [Fact]
            public async Task FullyCaughtUpWithMax()
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(DriverType, CursorValue.AddTicks(1));
                await SetCursorAsync(DriverType, CursorValue);

                // Act
                var result = await CatalogScanService.UpdateAsync(DriverType, max: CursorValue);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.FullyCaughtUpWithMax, result.Type);
                Assert.Null(result.Scan);
                Assert.Null(result.DependencyName);
            }

            [Fact]
            public async Task DoesNotRevertMinWhenMaxIsSet()
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(DriverType, CursorValue.AddTicks(1));
                await SetCursorAsync(DriverType, CursorValue);

                // Act
                var result = await CatalogScanService.UpdateAsync(DriverType, max: CursorValue.AddTicks(-1));

                // Assert
                Assert.Equal(CatalogScanServiceResultType.MinAfterMax, result.Type);
                Assert.Null(result.Scan);
                Assert.Null(result.DependencyName);
            }

            [Fact]
            public async Task ContinuesFromCursorValueWithNoMaxSpecified()
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                var first = CursorValue.AddMinutes(-10);
                await SetCursorAsync(DriverType, first);
                await SetDependencyCursorsAsync(DriverType, CursorValue);

                // Act
                var result = await CatalogScanService.UpdateAsync(DriverType);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
                Assert.Null(result.DependencyName);
                Assert.Equal(first, result.Scan.Min);
                Assert.Equal(CursorValue, result.Scan.Max);
            }

            [Fact]
            public async Task ContinuesFromCursorValueWithMaxSpecified()
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                var first = CursorValue.AddMinutes(-10);
                await SetCursorAsync(DriverType, first);
                await SetDependencyCursorsAsync(DriverType, CursorValue.AddMinutes(10));

                // Act
                var result = await CatalogScanService.UpdateAsync(DriverType, max: CursorValue);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
                Assert.Null(result.DependencyName);
                Assert.Equal(first, result.Scan.Min);
                Assert.Equal(CursorValue, result.Scan.Max);
            }

            [Theory]
            [MemberData(nameof(RejectsUnsupportedOnlyLatestLeavesData))]
            public async Task RejectsUnsupportedOnlyLatestLeaves(string typeName, bool badOnlyLatestLeaves)
            {
                // Arrange
                var type = CatalogScanDriverType.Parse(typeName);

                // Act & Assert
                var ex = await Assert.ThrowsAsync<ArgumentException>(() => CatalogScanService.UpdateAsync(type, max: null, badOnlyLatestLeaves));
                Assert.StartsWith(
                    badOnlyLatestLeaves
                        ? $"Only using all leaves is supported for driver {type}."
                        : $"Only using latest leaves is supported for driver {type}.",
                    ex.Message,
                    StringComparison.Ordinal);
            }

            public static IEnumerable<object[]> RejectsUnsupportedOnlyLatestLeavesData => TypeToInfo
                .Where(x => x.Value.OnlyLatestLeavesSupport.HasValue)
                .Select(x => new object[] { x.Key.ToString(), !x.Value.OnlyLatestLeavesSupport.Value });

            [Theory]
            [MemberData(nameof(SetsDefaultMin_WithAllLeavesData))]
            public async Task SetsDefaultMin_WithAllLeaves(string typeName, DateTimeOffset min)
            {
                // Arrange
                var type = CatalogScanDriverType.Parse(typeName);
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(type, CursorValue);

                // Act
                var result = await CatalogScanService.UpdateAsync(type, max: null, onlyLatestLeaves: false);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
                Assert.Equal(min, result.Scan.Min);
                Assert.Equal(CursorValue, result.Scan.Max);
                Assert.False(result.Scan.OnlyLatestLeaves);
            }

            public static IEnumerable<object[]> SetsDefaultMin_WithAllLeavesData => TypeToInfo
                .Where(x => !x.Value.OnlyLatestLeavesSupport.GetValueOrDefault(false))
                .Select(x => new object[] { x.Key.ToString(), x.Value.DefaultMin });

            [Theory]
            [MemberData(nameof(SetsDefaultMin_WithOnlyLatestLeavesData))]
            public async Task SetsDefaultMin_WithOnlyLatestLeaves(string typeName, DateTimeOffset min)
            {
                // Arrange
                var type = CatalogScanDriverType.Parse(typeName);
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(type, CursorValue);

                // Act
                var result = await CatalogScanService.UpdateAsync(type, max: null, onlyLatestLeaves: true);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
                Assert.Equal(min, result.Scan.Min);
                Assert.Equal(CursorValue, result.Scan.Max);
                Assert.True(result.Scan.OnlyLatestLeaves);
            }

            public static IEnumerable<object[]> SetsDefaultMin_WithOnlyLatestLeavesData => TypeToInfo
                .Where(x => x.Value.OnlyLatestLeavesSupport.GetValueOrDefault(true))
                .Select(x => new object[] { x.Key.ToString(), x.Value.DefaultMin });

            [Theory]
            [MemberData(nameof(SetsDefaultMin_WithDefaultFindLatestData))]
            public async Task SetsDefaultMin_WithDefaultFindLatest(string typeName, DateTimeOffset min, bool onlyLatestLeaves)
            {
                // Arrange
                var type = CatalogScanDriverType.Parse(typeName);
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(type, CursorValue);

                // Act
                var result = await CatalogScanService.UpdateAsync(type);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
                Assert.Equal(min, result.Scan.Min);
                Assert.Equal(CursorValue, result.Scan.Max);
                Assert.Equal(onlyLatestLeaves, result.Scan.OnlyLatestLeaves);
            }

            public static IEnumerable<object[]> SetsDefaultMin_WithDefaultFindLatestData => TypeToInfo
                .Select(x => new object[] { x.Key.ToString(), x.Value.DefaultMin, x.Value.OnlyLatestLeavesSupport.GetValueOrDefault(true), });

            [Theory]
            [MemberData(nameof(StartabledDriverTypesData))]
            public async Task MaxAlignsWithDependency(string typeName)
            {
                // Arrange
                var type = CatalogScanDriverType.Parse(typeName);
                await CatalogScanService.InitializeAsync();
                await SetDependencyCursorsAsync(type, CursorValue);

                // Act
                var result = await CatalogScanService.UpdateAsync(type);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
                Assert.Equal(CursorValue, result.Scan.Max);
            }

            public TheUpdateAsyncMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public class TheUpdateAllAsyncMethod : CatalogScanServiceTest
        {
            [Fact]
            public async Task StartExpectedDependencylessDrivers()
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                FlatContainerCursor = CursorValue;

                // Act
                var results = await CatalogScanService.UpdateAllAsync(CursorValue);

                // Assert
                Assert.Equal(
                    CatalogScanDriverMetadata
                        .DriverTypesWithNoDependencies
                        .ToList(),
                    results
                        .Where(x => x.Value.Type == CatalogScanServiceResultType.NewStarted)
                        .Select(x => x.Key)
                        .OrderBy(x => x.ToString(), StringComparer.Ordinal)
                        .ToArray());
                Assert.Equal(
                    CatalogScanDriverMetadata.StartableDriverTypes,
                    results.Keys.Select(x => x).ToList());
            }

            [Fact]
            public async Task CanCompleteAllDriversWithUpdate()
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                FlatContainerCursor = CursorValue;
                var finished = new Dictionary<CatalogScanDriverType, CatalogScanServiceResult>();

                // Act & Assert
                int started;
                do
                {
                    started = 0;
                    foreach (var pair in await CatalogScanService.UpdateAllAsync(CursorValue))
                    {
                        if (pair.Value.Type == CatalogScanServiceResultType.NewStarted)
                        {
                            started++;
                            await SetCursorAsync(pair.Key, pair.Value.Scan.Max);
                            finished.Add(pair.Key, pair.Value);
                        }
                    }
                }
                while (started > 0);

                Assert.Equal(
                    CatalogScanDriverMetadata.StartableDriverTypes,
                    CatalogScanDriverMetadata.SortByTopologicalOrder(finished.Keys, x => x).ToArray());
                Assert.All(finished.Values, x => Assert.Equal(CursorValue, x.Scan.Max));
            }

            public TheUpdateAllAsyncMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public class TypeToInfoTest : CatalogScanServiceTest
        {
            public TypeToInfoTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Theory]
            [MemberData(nameof(StartabledDriverTypesData))]
            public async Task SetDependencyCursorAsyncMatchesDeclaredDependencies(string typeName)
            {
                // Arrange
                var type = CatalogScanDriverType.Parse(typeName);
                var min = new DateTimeOffset(2024, 8, 13, 11, 50, 0, TimeSpan.Zero);
                FlatContainerCursor = CursorTableEntity.Min;
                await CatalogScanCursorService.InitializeAsync();

                // Act
                await SetDependencyCursorsAsync(type, min);

                // Assert
                var cursors = await CatalogScanCursorService.GetCursorsAsync();
                var dependencies = CatalogScanDriverMetadata.GetDependencies(type);
                if (dependencies.Any())
                {
                    // set
                    foreach (var dependency in dependencies)
                    {
                        Logger.LogInformation("Checking cursor for {Dependency}.", dependency);
                        Assert.Contains(dependency, cursors.Keys);
                        Assert.Equal(min, cursors[dependency].Value);
                        cursors.Remove(dependency);
                    }

                    // not set
                    Assert.Equal(CursorTableEntity.Min, FlatContainerCursor);
                    foreach (var nonDependency in cursors.Keys)
                    {
                        Logger.LogInformation("Checking cursor for {NonDependency}.", nonDependency);
                        Assert.Equal(CursorTableEntity.Min, cursors[nonDependency].Value);
                    }
                }
                else
                {
                    // set
                    Assert.Equal(min, FlatContainerCursor);

                    // not set
                    foreach (var nonDependency in cursors.Keys)
                    {
                        Logger.LogInformation("Checking cursor for {NonDependency}.", nonDependency);
                        Assert.Equal(CursorTableEntity.Min, cursors[nonDependency].Value);
                    }
                }
            }
        }

        private async Task SetDependencyCursorsAsync(CatalogScanDriverType type, DateTimeOffset min)
        {
            Assert.Contains(type, TypeToInfo.Keys);
            await TypeToInfo[type].SetDependencyCursorAsync(this, min);
        }

        /// <summary>
        /// Reflect on all static DriverInfo property of type DriverInfo to get the info for each driver.
        /// </summary>
        private static Dictionary<CatalogScanDriverType, DriverInfo> TypeToInfo => typeof(DriverInfo)
            .GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.GetProperty)
            .Where(x => x.PropertyType == typeof(DriverInfo))
            .ToDictionary(x => CatalogScanDriverType.Parse(x.Name), x => (DriverInfo)x.GetValue(null));

        public Mock<IRemoteCursorClient> RemoteCursorClient { get; }
        public DateTimeOffset FlatContainerCursor { get; set; }
        public DateTimeOffset CursorValue { get; }
        public CatalogScanDriverType DriverType { get; set; }

        public CatalogScanServiceTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            RemoteCursorClient = new Mock<IRemoteCursorClient>();

            FlatContainerCursor = DateTimeOffset.Parse("2021-02-02T16:00:00Z", CultureInfo.InvariantCulture);
            CursorValue = DateTimeOffset.Parse("2021-02-01T16:00:00Z", CultureInfo.InvariantCulture);
            DriverType = CatalogScanDriverType.PackageAssetToCsv;

            RemoteCursorClient.Setup(x => x.GetFlatContainerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => FlatContainerCursor);
        }

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddSingleton(RemoteCursorClient.Object);
            });
        }

        private partial class DriverInfo
        {
            public required DateTimeOffset DefaultMin { get; init; }
            public required bool? OnlyLatestLeavesSupport { get; init; }
            public required bool SupportsBucketRangeProcessing { get; init; }
            public required Func<CatalogScanServiceTest, DateTimeOffset, Task> SetDependencyCursorAsync { get; init; }
        }
    }
}

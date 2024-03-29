// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;

namespace NuGet.Insights.Worker
{
    public class CatalogScanServiceTest : BaseWorkerLogicIntegrationTest
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
                Assert.Contains($"{Options.Value.CatalogPageScanTableName}{scan.StorageSuffix}", tablesBefore);
                Assert.Contains($"{Options.Value.CatalogLeafScanTableName}{scan.StorageSuffix}", tablesBefore);
                Assert.Contains($"{Options.Value.CsvRecordTableName}{scan.StorageSuffix}0", tablesBefore);
                Assert.Contains($"{Options.Value.CsvRecordTableName}{scan.StorageSuffix}1", tablesBefore);
                Assert.Contains($"{Options.Value.CsvRecordTableName}{scan.StorageSuffix}2", tablesBefore);
                Assert.Contains($"{Options.Value.TaskStateTableName}{scan.StorageSuffix}", tablesBefore);
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
                Assert.Equal(CatalogScanServiceResultType.AlreadyRunning, result.Type);
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
            public async Task FailsToStartWithBuckets(CatalogScanDriverType driverType)
            {
                // Arrange & Act & Assert
                var ex = await Assert.ThrowsAsync<ArgumentException>(() => CatalogScanService.UpdateAsync(
                    ScanId,
                    StorageSuffix,
                    driverType,
                    Buckets));
                Assert.StartsWith(
                    $"The driver {driverType} is not supported for bucket range processing.",
                    ex.Message,
                    StringComparison.Ordinal);
            }

            public static IEnumerable<object[]> FailsToStartWithBucketsData => TypeToInfo
                .Where(x => !x.Value.SupportsBucketRangeProcessing)
                .Select(x => new object[] { x.Key });

            [Theory]
            [MemberData(nameof(StartsWithBucketsData))]
            public async Task StartsWithBuckets(CatalogScanDriverType driverType)
            {
                // Arrange
                await CatalogScanService.InitializeAsync();
                var expectedMax = new DateTimeOffset(2023, 11, 22, 9, 37, 13, TimeSpan.Zero);
                await SetDependencyCursorsAsync(driverType, expectedMax);
                await SetCursorsAsync([CatalogScanDriverType.LoadBucketedPackage, driverType], expectedMax);

                // Act
                var result = await CatalogScanService.UpdateAsync(ScanId, StorageSuffix, driverType, Buckets);

                // Assert
                Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
                Assert.Equal(driverType, result.Scan.DriverType);
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
                .Select(x => new object[] { x.Key });

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
                Assert.Equal(CatalogScanServiceResultType.AlreadyRunning, result.Type);
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
            public async Task RejectsUnsupportedOnlyLatestLeaves(CatalogScanDriverType type, bool badOnlyLatestLeaves)
            {
                // Arrange & Act & Assert
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
                .Select(x => new object[] { x.Key, !x.Value.OnlyLatestLeavesSupport.Value });

            [Theory]
            [MemberData(nameof(SetsDefaultMin_WithAllLeavesData))]
            public async Task SetsDefaultMin_WithAllLeaves(CatalogScanDriverType type, DateTimeOffset min)
            {
                // Arrange
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
                .Select(x => new object[] { x.Key, x.Value.DefaultMin });

            [Theory]
            [MemberData(nameof(SetsDefaultMin_WithOnlyLatestLeavesData))]
            public async Task SetsDefaultMin_WithOnlyLatestLeaves(CatalogScanDriverType type, DateTimeOffset min)
            {
                // Arrange
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
                .Select(x => new object[] { x.Key, x.Value.DefaultMin });

            [Theory]
            [MemberData(nameof(SetsDefaultMin_WithDefaultFindLatestData))]
            public async Task SetsDefaultMin_WithDefaultFindLatest(CatalogScanDriverType type, DateTimeOffset min, bool onlyLatestLeaves)
            {
                // Arrange
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
                .Select(x => new object[] { x.Key, x.Value.DefaultMin, x.Value.OnlyLatestLeavesSupport.GetValueOrDefault(true), });

            [Theory]
            [MemberData(nameof(StartabledDriverTypesData))]
            public async Task MaxAlignsWithDependency(CatalogScanDriverType type)
            {
                // Arrange
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
                    new[]
                    {
                        CatalogScanDriverType.BuildVersionSet,
                        CatalogScanDriverType.CatalogDataToCsv,
                        CatalogScanDriverType.LoadBucketedPackage,
                        CatalogScanDriverType.LoadLatestPackageLeaf,
                        CatalogScanDriverType.LoadPackageArchive,
                        CatalogScanDriverType.LoadPackageManifest,
                        CatalogScanDriverType.LoadPackageReadme,
                        CatalogScanDriverType.LoadPackageVersion,
                        CatalogScanDriverType.LoadSymbolPackageArchive,
    #if ENABLE_NPE
                        CatalogScanDriverType.NuGetPackageExplorerToCsv,
    #endif
                        CatalogScanDriverType.PackageAssemblyToCsv,
                        CatalogScanDriverType.PackageIconToCsv,
                        CatalogScanDriverType.PackageLicenseToCsv,
                    },
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
                    finished.Keys.Order().ToArray());
                Assert.All(finished.Values, x => Assert.Equal(CursorValue, x.Scan.Max));
            }

            public TheUpdateAllAsyncMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        private async Task SetDependencyCursorsAsync(CatalogScanDriverType type, DateTimeOffset min)
        {
            Assert.Contains(type, TypeToInfo.Keys);
            await TypeToInfo[type].SetDependencyCursorAsync(this, min);
        }

        private static Dictionary<CatalogScanDriverType, DriverInfo> TypeToInfo => new Dictionary<CatalogScanDriverType, DriverInfo>
        {
            {
                CatalogScanDriverType.BuildVersionSet,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = false,
                    SupportsBucketRangeProcessing = false,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.FlatContainerCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

            {
                CatalogScanDriverType.CatalogDataToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMin,
                    OnlyLatestLeavesSupport = false,
                    SupportsBucketRangeProcessing = false,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.FlatContainerCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

            {
                CatalogScanDriverType.LoadLatestPackageLeaf,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = false,
                    SupportsBucketRangeProcessing = false,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.FlatContainerCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

            {
                CatalogScanDriverType.LoadBucketedPackage,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = false,
                    SupportsBucketRangeProcessing = false,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.FlatContainerCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

            {
                CatalogScanDriverType.LoadPackageArchive,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.FlatContainerCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

            {
                CatalogScanDriverType.LoadSymbolPackageArchive,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.FlatContainerCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

            {
                CatalogScanDriverType.LoadPackageManifest,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.FlatContainerCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

            {
                CatalogScanDriverType.LoadPackageReadme,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.FlatContainerCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

            {
                CatalogScanDriverType.LoadPackageVersion,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = false,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.FlatContainerCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

#if ENABLE_NPE
            {
                CatalogScanDriverType.NuGetPackageExplorerToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.FlatContainerCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },
#endif

            {
                CatalogScanDriverType.PackageAssemblyToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.FlatContainerCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

            {
                CatalogScanDriverType.PackageAssetToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                    },
                }
            },

            {
                CatalogScanDriverType.PackageArchiveToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                        await self.SetCursorAsync(CatalogScanDriverType.PackageAssemblyToCsv, x);
                    },
                }
            },

#if ENABLE_CRYPTOAPI
            {
                CatalogScanDriverType.PackageCertificateToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                    },
                }
            },
#endif

            {
                CatalogScanDriverType.PackageCompatibilityToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageManifest, x);
                    },
                }
            },

            {
                CatalogScanDriverType.PackageContentToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                    },
                }
            },

            {
                CatalogScanDriverType.PackageLicenseToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.FlatContainerCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

            {
                CatalogScanDriverType.PackageIconToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.FlatContainerCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

            {
                CatalogScanDriverType.PackageManifestToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageManifest, x);
                    },
                }
            },

            {
                CatalogScanDriverType.PackageReadmeToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageReadme, x);
                    },
                }
            },

            {
                CatalogScanDriverType.PackageSignatureToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                    },
                }
            },

            {
                CatalogScanDriverType.PackageVersionToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = true,
                    SupportsBucketRangeProcessing = false,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageVersion, x);
                    },
                }
            },

            {
                CatalogScanDriverType.SymbolPackageArchiveToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    OnlyLatestLeavesSupport = null,
                    SupportsBucketRangeProcessing = true,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadSymbolPackageArchive, x);
                    },
                }
            },
        };

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

        private class DriverInfo
        {
            public required DateTimeOffset DefaultMin { get; init; }
            public required bool? OnlyLatestLeavesSupport { get; init; }
            public required bool SupportsBucketRangeProcessing { get; init; }
            public required Func<CatalogScanServiceTest, DateTimeOffset, Task> SetDependencyCursorAsync { get; init; }
        }
    }
}

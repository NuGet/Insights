using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanServiceTest : BaseWorkerLogicIntegrationTest
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
        [MemberData(nameof(SetsDefaultMinData))]
        public async Task SetsDefaultMin(CatalogScanDriverType type, DateTimeOffset expected)
        {
            // Arrange
            await CatalogScanService.InitializeAsync();
            await SetDependencyCursorsAsync(type, CursorValue);

            // Act
            var result = await CatalogScanService.UpdateAsync(type);

            // Assert
            Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
            Assert.Equal(expected, result.Scan.Min);
        }

        public static IEnumerable<object[]> SetsDefaultMinData => TypeToInfo
            .Select(x => new object[] { x.Key, x.Value.DefaultMin });

        [Fact]
        public async Task StartExpectedDependencylessDrivers()
        {
            // Arrange
            await CatalogScanService.InitializeAsync();
            FlatContainerCursor = CursorValue;

            // Act
            var results = await CatalogScanService.UpdateAsync(CursorValue);

            // Assert
            Assert.Equal(
                new[]
                {
                    CatalogScanDriverType.CatalogLeafItemToCsv,
                    CatalogScanDriverType.LoadLatestPackageLeaf,
                    CatalogScanDriverType.LoadPackageArchive,
                    CatalogScanDriverType.LoadPackageManifest,
                    CatalogScanDriverType.LoadPackageVersion,
                },
                results
                    .Where(x => x.Value.Type == CatalogScanServiceResultType.NewStarted)
                    .Select(x => x.Key)
                    .OrderBy(x => x.ToString())
                    .ToArray());
            Assert.Equal(
                CatalogScanCursorService.StartableDriverTypes,
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
                foreach (var pair in await CatalogScanService.UpdateAsync(CursorValue))
                {
                    if (pair.Value.Type == CatalogScanServiceResultType.NewStarted)
                    {
                        started++;
                        await SetCursorAsync(pair.Key, pair.Value.Scan.Max.Value);
                        finished.Add(pair.Key, pair.Value);
                    }
                }
            }
            while (started > 0);

            Assert.Equal(
                CatalogScanCursorService.StartableDriverTypes,
                finished.Keys.OrderBy(x => x).ToArray());
            Assert.All(finished.Values, x => Assert.Equal(CursorValue, x.Scan.Max.Value));
        }

        [Theory]
        [MemberData(nameof(StartableTypes))]
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

        public static IEnumerable<object[]> StartableTypes => Enum
            .GetValues(typeof(CatalogScanDriverType))
            .Cast<CatalogScanDriverType>()
            .Where(x => x != CatalogScanDriverType.Internal_FindLatestCatalogLeafScan
                     && x != CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId)
            .Select(x => new object[] { x });

        private async Task SetDependencyCursorsAsync(CatalogScanDriverType type, DateTimeOffset min)
        {
            Assert.Contains(type, TypeToInfo.Keys);
            await TypeToInfo[type].SetDependencyCursorAsync(this, min);
        }

        private static Dictionary<CatalogScanDriverType, DriverInfo> TypeToInfo => new Dictionary<CatalogScanDriverType, DriverInfo>
        {
            {
                CatalogScanDriverType.LoadPackageArchive,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
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
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.CatalogCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

            {
                CatalogScanDriverType.PackageArchiveEntryToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                    },
                }
            },

            {
                CatalogScanDriverType.PackageAssemblyToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                    },
                }
            },

            {
                CatalogScanDriverType.PackageAssetToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                    },
                }
            },

            {
                CatalogScanDriverType.PackageSignatureToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                    },
                }
            },

            {
                CatalogScanDriverType.CatalogLeafItemToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMin,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.CatalogCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

            {
                CatalogScanDriverType.LoadLatestPackageLeaf,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    SetDependencyCursorAsync = (self, x) =>
                    {
                        self.CatalogCursor = x;
                        return Task.CompletedTask;
                    },
                }
            },

            {
                CatalogScanDriverType.PackageManifestToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageManifest, x);
                    },
                }
            },

            {
                CatalogScanDriverType.PackageVersionToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        await self.SetCursorAsync(CatalogScanDriverType.LoadPackageVersion, x);
                    },
                }
            },

            {
                CatalogScanDriverType.NuGetPackageExplorerToCsv,
                new DriverInfo
                {
                    DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                    SetDependencyCursorAsync = async (self, x) =>
                    {
                        self.FlatContainerCursor = x;
                        await self.SetCursorAsync(CatalogScanDriverType.LoadLatestPackageLeaf, x);
                    },
                }
            },
        };

        public Mock<IRemoteCursorClient> RemoteCursorClient { get; }
        public DateTimeOffset CatalogCursor { get; set; }
        public DateTimeOffset FlatContainerCursor { get; set; }
        public DateTimeOffset CursorValue { get; }
        public CatalogScanDriverType DriverType { get; }

        public CatalogScanServiceTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            RemoteCursorClient = new Mock<IRemoteCursorClient>();

            CatalogCursor = DateTimeOffset.Parse("2021-02-03T16:00:00Z");
            FlatContainerCursor = DateTimeOffset.Parse("2021-02-02T16:00:00Z");
            CursorValue = DateTimeOffset.Parse("2021-02-01T16:00:00Z");
            DriverType = CatalogScanDriverType.PackageAssetToCsv;

            RemoteCursorClient.Setup(x => x.GetCatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => CatalogCursor);
            RemoteCursorClient.Setup(x => x.GetFlatContainerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => FlatContainerCursor);
        }

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder.ConfigureServices(x =>
            {
                x.AddSingleton(RemoteCursorClient.Object);
            });
        }

        private class DriverInfo
        {
            public DateTimeOffset DefaultMin { get; set; }
            public Func<CatalogScanServiceTest, DateTimeOffset, Task> SetDependencyCursorAsync { get; set; }
        }
    }
}

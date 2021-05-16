using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageAssetToCsv
{
    public class PackageAssetToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageAsset>
    {
        private const string PackageAssetToCsvDir = nameof(PackageAssetToCsv);
        private const string PackageAssetToCsv_WithDeleteDir = nameof(PackageAssetToCsv_WithDelete);
        private const string PackageAssetToCsv_WithDuplicatesDir = nameof(PackageAssetToCsv_WithDuplicates);

        public class PackageAssetToCsv : PackageAssetToCsvIntegrationTest
        {
            public PackageAssetToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");
                var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageAssetToCsvDir, Step1, 0);
                await AssertOutputAsync(PackageAssetToCsvDir, Step1, 1);
                await AssertOutputAsync(PackageAssetToCsvDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(PackageAssetToCsvDir, Step2, 0);
                await AssertOutputAsync(PackageAssetToCsvDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(PackageAssetToCsvDir, Step2, 2);
            }
        }

        public class PackageAssetToCsv_WithoutBatching : PackageAssetToCsvIntegrationTest
        {
            public PackageAssetToCsv_WithoutBatching(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureWorkerSettings = x => x.AllowBatching = false;

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageAssetToCsvDir, Step1, 0);
                await AssertOutputAsync(PackageAssetToCsvDir, Step1, 1);
                await AssertOutputAsync(PackageAssetToCsvDir, Step1, 2);
            }
        }

        public class PackageAssetToCsv_WithDelete : PackageAssetToCsvIntegrationTest
        {
            public PackageAssetToCsv_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                MakeDeletedPackageAvailable();
                var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z");
                var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z");
                var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageAssetToCsv_WithDeleteDir, Step1, 0);
                await AssertOutputAsync(PackageAssetToCsv_WithDeleteDir, Step1, 1);
                await AssertOutputAsync(PackageAssetToCsv_WithDeleteDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(PackageAssetToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputAsync(PackageAssetToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(PackageAssetToCsv_WithDeleteDir, Step2, 2);
            }
        }

        public class PackageAssetToCsv_WithDuplicates_OnlyLatestLeaves : PackageAssetToCsvIntegrationTest
        {
            public PackageAssetToCsv_WithDuplicates_OnlyLatestLeaves(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public Task Execute()
            {
                return PackageAssetToCsv_WithDuplicates(batchProcessing: false);
            }
        }

        public class PackageAssetToCsv_WithDuplicates_AllLeaves : PackageAssetToCsvIntegrationTest
        {
            public PackageAssetToCsv_WithDuplicates_AllLeaves(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => Enumerable.Empty<CatalogScanDriverType>();

            [Fact]
            public async Task Execute()
            {
                await PackageAssetToCsv_WithDuplicates(batchProcessing: false);
            }
        }

        public class PackageAssetToCsv_WithDuplicates_BatchProcessing : PackageAssetToCsvIntegrationTest
        {
            public PackageAssetToCsv_WithDuplicates_BatchProcessing(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => Enumerable.Empty<CatalogScanDriverType>();

            [Fact]
            public Task Execute()
            {
                return PackageAssetToCsv_WithDuplicates(batchProcessing: true);
            }
        }

        public PackageAssetToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.PackageAssetContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageAssetToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        private async Task PackageAssetToCsv_WithDuplicates(bool batchProcessing)
        {
            ConfigureWorkerSettings = x =>
            {
                x.AppendResultStorageBucketCount = 1;
                x.RunAllCatalogScanDriversAsBatch = batchProcessing;
            };

            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssetToCsv_WithDuplicatesDir, Step1, 0);

            var duplicatePackageRequests = HttpMessageHandlerFactory
                .Requests
                .Where(x => x.RequestUri.AbsolutePath.EndsWith("/gosms.ge-sms-api.1.0.1.nupkg"))
                .ToList();
            var onlyLatestLeaves = LatestLeavesTypes.Contains(DriverType);
            Assert.Equal(onlyLatestLeaves ? 1 : 2, duplicatePackageRequests.Where(x => x.Method == HttpMethod.Head).Count());
            Assert.Equal(onlyLatestLeaves ? 1 : 2, duplicatePackageRequests.Where(x => x.Method == HttpMethod.Get).Count());
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

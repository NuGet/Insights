using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.FindLatestCatalogLeafScan
{
    public class FindLatestCatalogLeafScanIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string FindLatestCatalogLeafScanDir = nameof(Internal_FindLatestCatalogLeafScan);
        private const string FindLatestCatalogLeafScan_WithDuplicatesDir = nameof(Internal_FindLatestCatalogLeafScan_WithDuplicates);

        public class Internal_FindLatestCatalogLeafScan : FindLatestCatalogLeafScanIntegrationTest
        {
            public Internal_FindLatestCatalogLeafScan(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2020-12-27T05:06:30.4180312Z");
                var max1 = DateTimeOffset.Parse("2020-12-27T05:07:21.9968244Z");

                await CatalogScanService.InitializeAsync();

                // Act
                await UpdateAsync(min0, max1);

                // Assert
                await AssertOutputAsync(FindLatestCatalogLeafScanDir);
            }
        }

        public class Internal_FindLatestCatalogLeafScan_WithDuplicates : FindLatestCatalogLeafScanIntegrationTest
        {
            public Internal_FindLatestCatalogLeafScan_WithDuplicates(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z");

                await CatalogScanService.InitializeAsync();

                // Act
                await UpdateAsync(min0, max1);

                // Assert
                await AssertOutputAsync(FindLatestCatalogLeafScan_WithDuplicatesDir);
            }
        }

        private const string ParentStorageSuffix = "parentstoragesuffix";

        public FindLatestCatalogLeafScanIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.Internal_FindLatestCatalogLeafScan;
        public override bool OnlyLatestLeaves => false;
        public override bool OnlyLatestLeavesPerId => false;

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            yield return Options.Value.CatalogLeafScanTableName + ParentStorageSuffix;
        }

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            yield break;
        }

        private async Task UpdateAsync(DateTimeOffset min0, DateTimeOffset max1)
        {
            // Arrange
            var parentScan = new CatalogIndexScan("parent-cursor", "parent-scan-id", ParentStorageSuffix)
            {
                ParsedDriverType = CatalogScanDriverType.PackageAssetToCsv,
                DriverParameters = "parent-parameters",
                ParsedState = CatalogIndexScanState.Created,
                Min = DateTimeOffset.Parse("2020-01-01T00:00:00Z"),
                Max = DateTimeOffset.Parse("2020-01-02T00:00:00Z"),
            };

            await CatalogScanStorageService.InsertAsync(parentScan);
            ExpectedCatalogIndexScans.Add(parentScan);

            await (await GetLeafScanTableAsync()).CreateIfNotExistsAsync();

            var scan = await CatalogScanService.GetOrStartFindLatestCatalogLeafScanAsync(
                scanId: "flcls-scan-id",
                storageSuffix: "flclsstoragesuffix",
                parentScanMessage: new CatalogIndexScanMessage
                {
                    CursorName = parentScan.CursorName,
                    ScanId = parentScan.ScanId,
                },
                min0,
                max1);

            // Act
            await UpdateAsync(scan);
        }

        private async Task AssertOutputAsync(string dir)
        {
            await AssertEntityOutputAsync<CatalogLeafScan>(
                await GetLeafScanTableAsync(),
                dir,
                cleanEntity: x => x.Created = DateTimeOffset.Parse("2020-01-03T00:00:00Z"));
        }

        private async Task<TableClient> GetLeafScanTableAsync()
        {
            return await CatalogScanStorageService.GetLeafScanTableAsync(ParentStorageSuffix);
        }
    }
}

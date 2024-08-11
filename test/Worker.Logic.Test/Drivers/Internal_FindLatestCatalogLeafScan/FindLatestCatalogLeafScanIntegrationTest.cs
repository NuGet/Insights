// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker.FindLatestCatalogLeafScan
{
    public class FindLatestCatalogLeafScanIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string FindLatestCatalogLeafScanDir = nameof(Internal_FindLatestCatalogLeafScan);
        private const string FindLatestCatalogLeafScan_WithDuplicatesDir = nameof(Internal_FindLatestCatalogLeafScan_WithDuplicates);

        [Fact]
        public async Task Internal_FindLatestCatalogLeafScan()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-12-27T05:06:30.4180312Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-27T05:07:21.9968244Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();

            // Act
            await UpdateAsync(min0, max1);

            // Assert
            await AssertOutputAsync(FindLatestCatalogLeafScanDir);
        }

        [Fact]
        public async Task Internal_FindLatestCatalogLeafScan_WithDuplicates()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();

            // Act
            await UpdateAsync(min0, max1);

            // Assert
            await AssertOutputAsync(FindLatestCatalogLeafScan_WithDuplicatesDir);
        }

        private const string ParentStorageSuffix = "parentstoragesuffix";

        public FindLatestCatalogLeafScanIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.Internal_FindLatestCatalogLeafScan;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => Enumerable.Empty<CatalogScanDriverType>();
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            yield return Options.Value.CatalogLeafScanTableName + ParentStorageSuffix;
        }

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            yield break;
        }

        protected override IEnumerable<string> GetExpectedLeaseNames()
        {
            yield return $"Start-{DriverType}-{ExpectedCatalogIndexScans.First().ParentDriverType}";
        }

        private async Task UpdateAsync(DateTimeOffset min0, DateTimeOffset max1)
        {
            // Arrange
            var parentScan = new CatalogIndexScan(CatalogScanDriverType.PackageAssetToCsv, "parent-scan-id", ParentStorageSuffix)
            {
                State = CatalogIndexScanState.Created,
                CursorName = "parent-cursor",
                Min = DateTimeOffset.Parse("2020-01-01T00:00:00Z", CultureInfo.InvariantCulture),
                Max = DateTimeOffset.Parse("2020-01-02T00:00:00Z", CultureInfo.InvariantCulture),
            };

            await CatalogScanStorageService.InsertAsync(parentScan);
            ExpectedCatalogIndexScans.Add(parentScan);

            await (await GetLeafScanTableAsync()).CreateIfNotExistsAsync();

            var scan = await CatalogScanService.GetOrStartFindLatestCatalogLeafScanAsync(
                scanId: "flcls-scan-id",
                storageSuffix: "flclsstoragesuffix",
                parentScan.DriverType,
                parentScan.ScanId,
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
                cleanEntity: x => x.Created = DateTimeOffset.Parse("2020-01-03T00:00:00Z", CultureInfo.InvariantCulture));
        }

        private async Task<TableClientWithRetryContext> GetLeafScanTableAsync()
        {
            return await CatalogScanStorageService.GetLeafScanTableAsync(ParentStorageSuffix);
        }
    }
}

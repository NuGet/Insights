// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker.FindLatestCatalogLeafScanPerId
{
    public class FindLatestCatalogLeafScanPerIdIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string FindLatestCatalogLeafScanPerIdDir = nameof(Internal_FindLatestCatalogLeafScanPerId);
        private const string FindLatestCatalogLeafScanPerId_WithDuplicatesDir = nameof(Internal_FindLatestCatalogLeafScanPerId_WithDuplicates);

        [Fact]
        public async Task Internal_FindLatestCatalogLeafScanPerId()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2024-09-01T04:28:13.0633324Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2024-09-01T04:28:45.9502500Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();

            // Act
            await UpdateAsync(min0, max1);

            // Assert
            await AssertOutputAsync(FindLatestCatalogLeafScanPerIdDir);
        }

        [Fact]
        public async Task Internal_FindLatestCatalogLeafScanPerId_WithDuplicates()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2024-06-04T03:21:17.1900080Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2024-06-04T03:22:18.9563713Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();

            // Act
            await UpdateAsync(min0, max1);

            // Assert
            await AssertOutputAsync(FindLatestCatalogLeafScanPerId_WithDuplicatesDir);
        }

        private const string ParentStorageSuffix = "parentstoragesuffix";

        public FindLatestCatalogLeafScanPerIdIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId;
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
            var parentScan = new CatalogIndexScan(CatalogScanDriverType.PackageVersionToCsv, "parent-scan-id", ParentStorageSuffix)
            {
                State = CatalogIndexScanState.Created,
                CursorName = "parent-cursor",
                Min = DateTimeOffset.Parse("2020-01-01T00:00:00Z", CultureInfo.InvariantCulture),
                Max = DateTimeOffset.Parse("2020-01-02T00:00:00Z", CultureInfo.InvariantCulture),
            };

            await CatalogScanStorageService.InsertAsync(parentScan);
            ExpectedCatalogIndexScans.Add(parentScan);

            await (await GetLeafScanTableAsync()).CreateIfNotExistsAsync();

            var scan = await CatalogScanService.GetOrStartFindLatestCatalogLeafScanPerIdAsync(
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

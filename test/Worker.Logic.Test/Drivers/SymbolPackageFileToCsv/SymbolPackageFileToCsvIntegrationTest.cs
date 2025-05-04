// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.SymbolPackageFileToCsv
{
    public class SymbolPackageFileToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<SymbolPackageFileRecord>
    {
        private const string SymbolPackageFileToCsvDir = nameof(SymbolPackageFileToCsv);
        private const string SymbolPackageFileToCsv_WithDeleteDir = nameof(SymbolPackageFileToCsv_WithDelete);

        [Fact]
        public async Task SymbolPackageFileToCsv()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2021-03-22T20:13:00.3409860Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2021-03-22T20:13:54.6075418Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2021-03-22T20:15:23.6403188Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadSymbolPackageArchive, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(SymbolPackageFileToCsvDir, Step1, 0);
            await AssertSymbolPackageHashesTableAsync(SymbolPackageFileToCsvDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(SymbolPackageFileToCsvDir, Step2, 0);
            await AssertSymbolPackageHashesTableAsync(SymbolPackageFileToCsvDir, Step2);
        }

        [Fact]
        public async Task SymbolPackageFileToCsv_WithDelete()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadSymbolPackageArchive, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(SymbolPackageFileToCsv_WithDeleteDir, Step1, 0);
            await AssertSymbolPackageHashesTableAsync(SymbolPackageFileToCsv_WithDeleteDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(SymbolPackageFileToCsv_WithDeleteDir, Step2, 0);
            await AssertSymbolPackageHashesTableAsync(SymbolPackageFileToCsv_WithDeleteDir, Step2);
        }

        public SymbolPackageFileToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.SymbolPackageFileContainer;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.SymbolPackageFileToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => [DriverType];
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[]
            {
                "CatalogScan-" + CatalogScanDriverType.LoadSymbolPackageArchive,
            });
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(
            [
                Options.Value.SymbolPackageArchiveTable,
                Options.Value.SymbolPackageHashesTable,
            ]);
        }
    }
}

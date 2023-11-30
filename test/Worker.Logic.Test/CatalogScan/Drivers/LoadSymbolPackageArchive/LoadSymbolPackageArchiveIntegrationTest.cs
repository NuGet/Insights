// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.LoadSymbolPackageArchive
{
    public class LoadSymbolPackageArchiveIntegrationTest : BaseCatalogScanIntegrationTest
    {
        [Fact]
        public async Task Simple()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2021-03-22T20:13:00.3409860Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2021-03-22T20:13:54.6075418Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2021-03-22T20:15:23.6403188Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await Verify(await GetSymbolPackageArchiveTableAsync(step: 1));

            // Act
            await UpdateAsync(max2);

            // Assert
            await Verify(await GetSymbolPackageArchiveTableAsync(step: 2)).DisableRequireUniquePrefix();
        }

        [Fact]
        public async Task Delete()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await Verify(await GetSymbolPackageArchiveTableAsync(step: 1));

            // Act
            await UpdateAsync(max2);

            // Assert
            await Verify(await GetSymbolPackageArchiveTableAsync(step: 2)).DisableRequireUniquePrefix();
        }

        public LoadSymbolPackageArchiveIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.LoadSymbolPackageArchive;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.SymbolPackageArchiveTableName });
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.LoadPackageManifest
{
    public class LoadPackageManifestIntegrationTest : BaseCatalogScanIntegrationTest
    {
        [Fact]
        public async Task Simple()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await Verify(await GetPackageManifestTableAsync(step: 1));

            // Act
            await UpdateAsync(max2);

            // Assert
            await Verify(await GetPackageManifestTableAsync(step: 2)).DisableRequireUniquePrefix();
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
            await Verify(await GetPackageManifestTableAsync(step: 1));

            // Act
            await UpdateAsync(max2);

            // Assert
            await Verify(await GetPackageManifestTableAsync(step: 2)).DisableRequireUniquePrefix();
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageManifestTableName });
        }

        public LoadPackageManifestIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.LoadPackageManifest;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();
    }
}

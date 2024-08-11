// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.LoadPackageVersion
{
    public class LoadPackageVersionIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string LoadPackageVersionDir = nameof(LoadPackageVersion);
        private const string LoadPackageVersion_WithDeleteDir = nameof(LoadPackageVersion_WithDelete);
        private const string LoadPackageVersion_WithDuplicatesDir = nameof(LoadPackageVersion_WithDuplicates);
        private const string LoadPackageVersion_SemVer2Dir = nameof(LoadPackageVersion_SemVer2);

        [Fact]
        public async Task LoadPackageVersion()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-12-27T05:06:30.4180312Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-27T05:07:21.9968244Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(LoadPackageVersionDir, Step1);
        }

        [Fact]
        public async Task LoadPackageVersion_WithDelete()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(LoadPackageVersion_WithDeleteDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(LoadPackageVersion_WithDeleteDir, Step2);
        }

        [Fact]
        public async Task LoadPackageVersion_WithDuplicates()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(LoadPackageVersion_WithDuplicatesDir, Step1);
        }

        [Fact]
        public async Task LoadPackageVersion_SemVer2()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2021-02-28T01:06:32.8546849Z", CultureInfo.InvariantCulture).AddTicks(-1);
            var max1 = DateTimeOffset.Parse("2021-02-28T01:06:32.8546849Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(LoadPackageVersion_SemVer2Dir, Step1);
        }

        public LoadPackageVersionIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.LoadPackageVersion;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => Enumerable.Empty<CatalogScanDriverType>();
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            yield return Options.Value.PackageVersionTableName;
        }

        private async Task AssertOutputAsync(string dir, string stepName)
        {
            var table = (await ServiceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(Options.Value.PackageVersionTableName);

            await AssertEntityOutputAsync<PackageVersionEntity>(table, Path.Combine(dir, stepName));
        }
    }
}

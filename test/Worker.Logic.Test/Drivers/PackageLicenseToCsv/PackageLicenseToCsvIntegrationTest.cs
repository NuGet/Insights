// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageLicenseToCsv
{
    public class PackageLicenseToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageLicense>
    {
        private const string PackageLicenseToCsvDir = nameof(PackageLicenseToCsv);
        private const string PackageLicenseToCsv_WithDeleteDir = nameof(PackageLicenseToCsv_WithDelete);

        [Fact]
        public async Task PackageLicenseToCsv()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2023-08-15T13:49:19.3162525Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2023-08-15T13:49:55.9593851Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2023-08-15T13:50:29.8678196Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageLicenseToCsvDir, Step1, 0);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageLicenseToCsvDir, Step2, 0);
        }

        [Fact]
        public async Task PackageLicenseToCsv_WithDelete()
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
            await AssertOutputAsync(PackageLicenseToCsv_WithDeleteDir, Step1, 0);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageLicenseToCsv_WithDeleteDir, Step2, 0);
        }

        public PackageLicenseToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.PackageLicenseContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageLicenseToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageIconToCsv
{
    public class PackageIconToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageIcon>
    {
        private const string PackageIconToCsvDir = nameof(PackageIconToCsv);
        private const string PackageIconToCsv_WithDeleteDir = nameof(PackageIconToCsv_WithDelete);

        [Fact]
        public async Task PackageIconToCsv()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-07-02T11:08:40.1879620Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-07-02T11:17:12.3930486Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-07-02T11:18:45.0838751Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageIconToCsvDir, Step1, 0);
            await AssertOutputAsync(PackageIconToCsvDir, Step1, 1);
            await AssertOutputAsync(PackageIconToCsvDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageIconToCsvDir, Step2, 0);
            await AssertOutputAsync(PackageIconToCsvDir, Step2, 1);
            await AssertOutputAsync(PackageIconToCsvDir, Step2, 2);
        }

        [Fact]
        public async Task PackageIconToCsv_WithDelete()
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
            await AssertOutputAsync(PackageIconToCsv_WithDeleteDir, Step1, 0);
            await AssertOutputAsync(PackageIconToCsv_WithDeleteDir, Step1, 1);
            await AssertOutputAsync(PackageIconToCsv_WithDeleteDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageIconToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
            await AssertOutputAsync(PackageIconToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
            await AssertOutputAsync(PackageIconToCsv_WithDeleteDir, Step2, 2);
        }

        public PackageIconToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.PackageIconContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageIconToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();
    }
}

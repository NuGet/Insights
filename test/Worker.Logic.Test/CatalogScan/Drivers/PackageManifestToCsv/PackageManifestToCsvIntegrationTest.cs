// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageManifestToCsv
{
    public class PackageManifestToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageManifestRecord>
    {
        private const string PackageManifestToCsvDir = nameof(PackageManifestToCsv);
        private const string PackageManifestToCsv_WithDeleteDir = nameof(PackageManifestToCsv_WithDelete);
        private const string PackageManifestToCsv_WithVeryLargeBufferDir = nameof(PackageManifestToCsv_WithVeryLargeBuffer);

        [Fact]
        public async Task PackageManifestToCsv()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageManifest, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageManifestToCsvDir, Step1, 0);
            await AssertOutputAsync(PackageManifestToCsvDir, Step1, 1);
            await AssertOutputAsync(PackageManifestToCsvDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageManifestToCsvDir, Step2, 0);
            await AssertOutputAsync(PackageManifestToCsvDir, Step1, 1); // This file is unchanged.
            await AssertOutputAsync(PackageManifestToCsvDir, Step2, 2);
        }

        [Fact]
        public async Task PackageManifestToCsv_WithDelete()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageManifest, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageManifestToCsv_WithDeleteDir, Step1, 0);
            await AssertOutputAsync(PackageManifestToCsv_WithDeleteDir, Step1, 1);
            await AssertOutputAsync(PackageManifestToCsv_WithDeleteDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageManifestToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
            await AssertOutputAsync(PackageManifestToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
            await AssertOutputAsync(PackageManifestToCsv_WithDeleteDir, Step2, 2);
        }

        [Fact]
        public async Task PackageManifestToCsv_WithVeryLargeBuffer()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

            var min0 = DateTimeOffset.Parse("2020-01-09T14:58:13.0458090Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-01-09T15:02:21.9360277Z", CultureInfo.InvariantCulture); // GR.PageRender.Razor 1.7.0
            var max2 = DateTimeOffset.Parse("2020-01-09T15:04:22.3831333Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageManifest, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageManifestToCsv_WithVeryLargeBufferDir, Step1, 0);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageManifestToCsv_WithVeryLargeBufferDir, Step2, 0);
        }

        public PackageManifestToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.PackageManifestContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageManifestToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[] { "CatalogScan-" + CatalogScanDriverType.LoadPackageManifest });
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageManifestTableName });
        }
    }
}

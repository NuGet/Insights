// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageReadmeToCsv
{
    public class PackageReadmeToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageReadme>
    {
        private const string PackageReadmeToCsvDir = nameof(PackageReadmeToCsv);
        private const string PackageReadmeToCsv_WithDeleteDir = nameof(PackageReadmeToCsv_WithDelete);
        private const string PackageReadmeToCsv_WithVeryLargeBufferDir = nameof(PackageReadmeToCsv_WithVeryLargeBuffer);

        [Fact]
        public async Task PackageReadmeToCsv()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2022-03-14T23:05:39.6122305Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2022-03-14T23:06:07.7549588Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2022-03-14T23:06:36.1633247Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageReadme, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageReadmeToCsvDir, Step1, 0);
            await AssertOutputAsync(PackageReadmeToCsvDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageReadmeToCsvDir, Step1, 0); // This file is unchanged
            await AssertOutputAsync(PackageReadmeToCsvDir, Step2, 2);
            await AssertCsvCountAsync(2);
        }

        [Fact]
        public async Task PackageReadmeToCsv_WithDelete()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageReadme, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageReadmeToCsv_WithDeleteDir, Step1, 0);
            await AssertOutputAsync(PackageReadmeToCsv_WithDeleteDir, Step1, 1);
            await AssertOutputAsync(PackageReadmeToCsv_WithDeleteDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageReadmeToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
            await AssertOutputAsync(PackageReadmeToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
            await AssertOutputAsync(PackageReadmeToCsv_WithDeleteDir, Step2, 2);
        }

        [Fact]
        public async Task PackageReadmeToCsv_WithVeryLargeBuffer()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

            var max1 = DateTimeOffset.Parse("2022-03-10T21:32:51.8317694Z", CultureInfo.InvariantCulture); // PodcastAPI 1.1.1
            var min0 = max1.AddTicks(-1);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageReadme, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageReadmeToCsv_WithVeryLargeBufferDir, Step1, 0);
        }

        public PackageReadmeToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.PackageReadmeContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageReadmeToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[] { "CatalogScan-" + CatalogScanDriverType.LoadPackageReadme });
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageReadmeTableName });
        }
    }
}

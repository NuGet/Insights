// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageSignatureToCsv
{
    public class PackageSignatureToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageSignature>
    {
        private const string PackageSignatureToCsvDir = nameof(PackageSignatureToCsv);
        private const string PackageSignatureToCsv_WithDuplicatesInCommitDir = nameof(PackageSignatureToCsv_WithDuplicatesInCommit);
        private const string PackageSignatureToCsv_AuthorSignatureDir = nameof(PackageSignatureToCsv_AuthorSignature);
        private const string PackageSignatureToCsv_BadTimestampDir = nameof(PackageSignatureToCsv_BadTimestamp);
        private const string PackageSignatureToCsv_BadTimestampEncodingDir = nameof(PackageSignatureToCsv_BadTimestampEncoding);
        private const string PackageSignatureToCsv_WithDeleteDir = nameof(PackageSignatureToCsv_WithDelete);

        [Fact]
        public async Task PackageSignatureToCsv()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageSignatureToCsvDir, Step1, 0);
            await AssertOutputAsync(PackageSignatureToCsvDir, Step1, 1);
            await AssertOutputAsync(PackageSignatureToCsvDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageSignatureToCsvDir, Step2, 0);
            await AssertOutputAsync(PackageSignatureToCsvDir, Step1, 1); // This file is unchanged.
            await AssertOutputAsync(PackageSignatureToCsvDir, Step2, 2);
        }

        [Fact]
        public async Task PackageSignatureToCsv_WithDuplicatesInCommit()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2018-03-23T08:55:02.1875809Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2018-03-23T08:55:20.0232708Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2018-03-23T08:55:38.0342003Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageSignatureToCsv_WithDuplicatesInCommitDir, Step1, 0);
            await AssertOutputAsync(PackageSignatureToCsv_WithDuplicatesInCommitDir, Step1, 1);
            await AssertOutputAsync(PackageSignatureToCsv_WithDuplicatesInCommitDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageSignatureToCsv_WithDuplicatesInCommitDir, Step1, 0); // This file is unchanged.
            await AssertOutputAsync(PackageSignatureToCsv_WithDuplicatesInCommitDir, Step2, 1);
            await AssertOutputAsync(PackageSignatureToCsv_WithDuplicatesInCommitDir, Step2, 2);
        }

        [Fact]
        public async Task PackageSignatureToCsv_AuthorSignature()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

            var min0 = DateTimeOffset.Parse("2020-03-04T22:55:23.8646211Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-03-04T22:56:51.1816512Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageSignatureToCsv_AuthorSignatureDir, Step1, 0);
        }

        [Fact]
        public async Task PackageSignatureToCsv_BadTimestamp()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

            var min0 = DateTimeOffset.Parse("2020-11-04T15:12:14Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-04T15:12:15.7221964Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageSignatureToCsv_BadTimestampDir, Step1, 0);
        }

        [Fact]
        public async Task PackageSignatureToCsv_BadTimestampEncoding()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

            var min0 = DateTimeOffset.Parse("2022-02-08T11:43:31.00000000Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2022-02-08T11:43:32.6621038Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            // .NET Runtime 6.0.0 fails to read the author signature of Avira.Managed.Remediation 1.2202.701. 6.0.5 succeeds.
            await AssertOutputAsync(PackageSignatureToCsv_BadTimestampEncodingDir, Step1, 0);
        }

        [Fact]
        public async Task PackageSignatureToCsv_WithDelete()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageSignatureToCsv_WithDeleteDir, Step1, 0);
            await AssertOutputAsync(PackageSignatureToCsv_WithDeleteDir, Step1, 1);
            await AssertOutputAsync(PackageSignatureToCsv_WithDeleteDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageSignatureToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
            await AssertOutputAsync(PackageSignatureToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
            await AssertOutputAsync(PackageSignatureToCsv_WithDeleteDir, Step2, 2);
        }

        public PackageSignatureToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.PackageSignatureContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageSignatureToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[] { "CatalogScan-" + CatalogScanDriverType.LoadPackageArchive });
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageArchiveTableName });
        }
    }
}

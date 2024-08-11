// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.LoadBucketedPackage
{
    public class LoadBucketedPackageIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string LoadBucketedPackageLeavesDir = nameof(LoadBucketedPackage);
        private const string LoadBucketedPackage_WithDuplicatesInCommitDir = nameof(LoadBucketedPackage_WithDuplicatesInCommit);

        [Fact]
        public async Task LoadBucketedPackage()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-12-27T05:06:30.4180312Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-27T05:07:21.9968244Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(LoadBucketedPackageLeavesDir, Step1);
        }

        [Fact]
        public async Task LoadBucketedPackage_WithDuplicatesInCommit()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2018-03-23T08:55:02.1875809Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2018-03-23T08:55:20.0232708Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2018-03-23T08:55:38.0342003Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(LoadBucketedPackage_WithDuplicatesInCommitDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(LoadBucketedPackage_WithDuplicatesInCommitDir, Step2);
        }

        public LoadBucketedPackageIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.LoadBucketedPackage;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => Enumerable.Empty<CatalogScanDriverType>();
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            yield return Options.Value.BucketedPackageTableName;
        }

        private async Task AssertOutputAsync(string dir, string step)
        {
            var table = (await ServiceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(Options.Value.BucketedPackageTableName);
            await AssertEntityOutputAsync<BucketedPackage>(table, Path.Combine(dir, step));
        }
    }
}

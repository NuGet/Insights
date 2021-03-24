using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.LoadLatestPackageLeaf
{
    public class LoadLatestPackageLeafIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string LoadLatestPackageLeavesDir = nameof(LoadLatestPackageLeaf);
        private const string LoadLatestPackageLeaves_WithDuplicatesWithDuplicatesInCommitDir = nameof(LoadLatestPackageLeaf_WithDuplicatesInCommit);

        public class LoadLatestPackageLeaf : LoadLatestPackageLeafIntegrationTest
        {
            public LoadLatestPackageLeaf(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2020-12-27T05:06:30.4180312Z");
                var max1 = DateTimeOffset.Parse("2020-12-27T05:07:21.9968244Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(LoadLatestPackageLeavesDir, Step1);
            }
        }

        public class LoadLatestPackageLeaf_WithDuplicatesInCommit : LoadLatestPackageLeafIntegrationTest
        {
            public LoadLatestPackageLeaf_WithDuplicatesInCommit(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2018-03-23T08:55:02.1875809Z");
                var max1 = DateTimeOffset.Parse("2018-03-23T08:55:20.0232708Z");
                var max2 = DateTimeOffset.Parse("2018-03-23T08:55:38.0342003Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(LoadLatestPackageLeaves_WithDuplicatesWithDuplicatesInCommitDir, Step1);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(LoadLatestPackageLeaves_WithDuplicatesWithDuplicatesInCommitDir, Step2);
            }
        }

        public LoadLatestPackageLeafIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.LoadLatestPackageLeaf;
        public override bool OnlyLatestLeaves => false;
        public override bool OnlyLatestLeavesPerId => false;

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            yield return Options.Value.LatestPackageLeafTableName;
        }

        private async Task AssertOutputAsync(string dir, string step)
        {
            var table = (await NewServiceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(Options.Value.LatestPackageLeafTableName);
            await AssertEntityOutputAsync<LatestPackageLeaf>(table, Path.Combine(dir, step));
        }
    }
}

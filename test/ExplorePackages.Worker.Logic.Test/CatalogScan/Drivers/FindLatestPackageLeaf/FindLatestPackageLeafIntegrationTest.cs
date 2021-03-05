using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.FindLatestPackageLeaf
{
    public class FindLatestPackageLeafIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string FindLatestPackageLeavesDir = nameof(FindLatestPackageLeaf);
        private const string FindLatestPackageLeaves_WithDuplicatesDir = nameof(FindLatestPackageLeaf_WithDuplicates);

        public class FindLatestPackageLeaf : FindLatestPackageLeafIntegrationTest
        {
            public FindLatestPackageLeaf(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-12-27T05:06:30.4180312Z");
                var max1 = DateTimeOffset.Parse("2020-12-27T05:07:21.9968244Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(FindLatestPackageLeavesDir, Step1);
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class FindLatestPackageLeaf_WithDuplicates : FindLatestPackageLeafIntegrationTest
        {
            public FindLatestPackageLeaf_WithDuplicates(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2018-03-23T08:55:02.1875809Z");
                var max1 = DateTimeOffset.Parse("2018-03-23T08:55:20.0232708Z");
                var max2 = DateTimeOffset.Parse("2018-03-23T08:55:38.0342003Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(FindLatestPackageLeaves_WithDuplicatesDir, Step1);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(FindLatestPackageLeaves_WithDuplicatesDir, Step2);

                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public FindLatestPackageLeafIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.FindLatestPackageLeaf;
        public override bool OnlyLatestLeaves => false;
        public override bool OnlyLatestLeavesPerId => false;

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            yield return Options.Value.LatestPackageLeafTableName;
        }

        private async Task AssertOutputAsync(string dir, string step)
        {
            var table = ServiceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(Options.Value.LatestPackageLeafTableName);
            await AssertEntityOutputAsync<LatestPackageLeaf>(table, Path.Combine(dir, step));
        }
    }
}

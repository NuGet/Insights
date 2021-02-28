using System;
using System.Collections.Generic;
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

        public FindLatestPackageLeafIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.FindLatestPackageLeaf;

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
                await AssertOutputAsync(FindLatestPackageLeavesDir);
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
                var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(FindLatestPackageLeaves_WithDuplicatesDir);
                AssertOnlyInfoLogsOrLess();
            }
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            yield return Options.Value.LatestPackageLeafTableName;
        }

        private async Task AssertOutputAsync(string dir)
        {
            var table = ServiceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(Options.Value.LatestPackageLeafTableName);
            await AssertEntityOutputAsync<LatestPackageLeaf>(table, dir);
        }
    }
}

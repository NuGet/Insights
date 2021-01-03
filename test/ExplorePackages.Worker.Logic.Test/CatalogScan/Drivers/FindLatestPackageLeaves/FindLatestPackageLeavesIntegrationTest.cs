using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.FindLatestPackageLeaves;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.FindLatestLeaves
{
    public class FindLatestPackageLeavesIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string FindLatestPackageLeavesDir = nameof(FindLatestPackageLeaves);
        private const string FindLatestPackageLeaves_WithDuplicatesDir = nameof(FindLatestPackageLeaves_WithDuplicates);

        public FindLatestPackageLeavesIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.FindLatestPackageLeaves;

        public class FindLatestPackageLeaves : FindLatestPackageLeavesIntegrationTest
        {
            public FindLatestPackageLeaves(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await VerifyOutputAsync(FindLatestPackageLeavesDir);
            }
        }

        public class FindLatestPackageLeaves_WithDuplicates : FindLatestPackageLeavesIntegrationTest
        {
            public FindLatestPackageLeaves_WithDuplicates(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await VerifyOutputAsync(FindLatestPackageLeaves_WithDuplicatesDir);
            }
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            yield return Options.Value.LatestPackageLeavesTableName;
        }

        private async Task VerifyOutputAsync(string dir)
        {
            var table = ServiceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(Options.Value.LatestPackageLeavesTableName);
            await VerifyOutputAsync<LatestPackageLeaf>(table, dir);
        }
    }
}

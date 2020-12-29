using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.FindCatalogLeafItems
{
    public class FindCatalogLeafItemsIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string FindCatalogLeafItemsDir = nameof(FindCatalogLeafItems);

        public FindCatalogLeafItemsIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.FindCatalogLeafItemsContainerName;
        protected override CatalogScanType Type => CatalogScanType.FindCatalogLeafItems;

        public class FindCatalogLeafItems : FindCatalogLeafItemsIntegrationTest
        {
            public FindCatalogLeafItems(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-12-27T05:06:30.4180312Z");
                var max1 = DateTimeOffset.Parse("2020-12-27T05:07:45.7628472Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(FindCatalogLeafItemsDir, Step1, 0);

                await VerifyExpectedContainersAsync();
            }
        }
    }
}

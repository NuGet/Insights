using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.FindCatalogLeafItem
{
    public class FindCatalogLeafItemIntegrationTest : BaseCatalogScanToCsvIntegrationTest
    {
        private const string FindCatalogLeafItemDir = nameof(FindCatalogLeafItem);
        private const string FindCatalogLeafItem_WithDuplicatesDir = nameof(FindCatalogLeafItem_WithDuplicates);

        public FindCatalogLeafItemIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.CatalogLeafItemContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.FindCatalogLeafItem;

        public class FindCatalogLeafItem : FindCatalogLeafItemIntegrationTest
        {
            public FindCatalogLeafItem(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await AssertOutputAsync(FindCatalogLeafItemDir, Step1, 0);
                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class FindCatalogLeafItem_WithDuplicates : FindCatalogLeafItemIntegrationTest
        {
            public FindCatalogLeafItem_WithDuplicates(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(FindCatalogLeafItem_WithDuplicatesDir, Step1, 0);
                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }
    }
}

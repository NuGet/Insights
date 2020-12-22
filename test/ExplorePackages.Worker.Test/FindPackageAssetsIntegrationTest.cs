using System;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.Support;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public class FindPackageAssetsIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string FindPackageAssetsDir = nameof(FindPackageAssets);
        private const string FindPackageAssets_WithDeleteDir = nameof(FindPackageAssets_WithDelete);

        public FindPackageAssetsIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.FindPackageAssetsContainerName;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task FindPackageAssets(bool allowBatching)
        {
            ConfigureWorkerSettings = x => x.AllowBatching = allowBatching;

            Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z");
            var cursorName = $"CatalogScan-{CatalogScanType.FindPackageAssets}";

            await CatalogScanService.InitializeAsync();

            var cursor = await CursorStorageService.GetOrCreateAsync(cursorName);
            cursor.Value = min0;
            await CursorStorageService.UpdateAsync(cursor);

            // Act
            await UpdateFindPackageAssetsAsync(max1);

            // Assert
            await AssertOutputAsync(FindPackageAssetsDir, Step1, 0);
            await AssertOutputAsync(FindPackageAssetsDir, Step1, 1);
            await AssertOutputAsync(FindPackageAssetsDir, Step1, 2);

            // Act
            await UpdateFindPackageAssetsAsync(max2);

            // Assert
            await AssertOutputAsync(FindPackageAssetsDir, Step2, 0);
            await AssertOutputAsync(FindPackageAssetsDir, Step1, 1); // This file is unchanged.
            await AssertOutputAsync(FindPackageAssetsDir, Step2, 2);

            await VerifyExpectedContainers();
        }

        [Fact]
        public async Task FindPackageAssets_WithDelete()
        {
            Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

            // Arrange
            HttpMessageHandlerFactory.OnSendAsync = async req =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/behaviorsample.1.0.0.nupkg"))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestData}/behaviorsample.1.0.0.nupkg");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z");
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z");
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z");
            var cursorName = $"CatalogScan-{CatalogScanType.FindPackageAssets}";

            await CatalogScanService.InitializeAsync();

            var cursor = await CursorStorageService.GetOrCreateAsync(cursorName);
            cursor.Value = min0;
            await CursorStorageService.UpdateAsync(cursor);

            // Act
            await UpdateFindPackageAssetsAsync(max1);

            // Assert
            await AssertOutputAsync(FindPackageAssets_WithDeleteDir, Step1, 0);
            await AssertOutputAsync(FindPackageAssets_WithDeleteDir, Step1, 1);
            await AssertOutputAsync(FindPackageAssets_WithDeleteDir, Step1, 2);

            // Act
            await UpdateFindPackageAssetsAsync(max2);

            // Assert
            await AssertOutputAsync(FindPackageAssets_WithDeleteDir, Step1, 0); // This file is unchanged.
            await AssertOutputAsync(FindPackageAssets_WithDeleteDir, Step1, 1); // This file is unchanged.
            await AssertOutputAsync(FindPackageAssets_WithDeleteDir, Step2, 2);

            await VerifyExpectedContainers();
        }

        private async Task UpdateFindPackageAssetsAsync(DateTimeOffset max)
        {
            var indexScan = await CatalogScanService.UpdateFindPackageAssetsAsync(max);
            await ProcessQueueAsync(indexScan);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;

namespace NuGet.Insights.Worker.PopularityTransfersToCsv
{
    public class PopularityTransfersToCsvIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public const string PopularityTransfersToCsvDir = nameof(PopularityTransfersToCsv);
        private const string PopularityTransfersToCsv_NonExistentIdDir = nameof(PopularityTransfersToCsv_NonExistentId);
        private const string PopularityTransfersToCsv_UncheckedIdDir = nameof(PopularityTransfersToCsv_UncheckedId);

        [Fact]
        public async Task PopularityTransfersToCsv()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PopularityTransfer>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(PopularityTransfersToCsvDir, Step1, "popularity_transfers_08585907868854775807.csv.gz");
            await AssertCsvBlobAsync(PopularityTransfersToCsvDir, Step1, "latest_popularity_transfers.csv.gz");

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(3);
            await AssertCsvBlobAsync(PopularityTransfersToCsvDir, Step1, "popularity_transfers_08585907868854775807.csv.gz");
            await AssertCsvBlobAsync(PopularityTransfersToCsvDir, Step2, "popularity_transfers_08585906968854775807.csv.gz");
            await AssertCsvBlobAsync(PopularityTransfersToCsvDir, Step2, "latest_popularity_transfers.csv.gz");
        }

        [Fact]
        public async Task PopularityTransfersToCsv_NoOp()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PopularityTransfer>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(PopularityTransfersToCsvDir, Step1, "latest_popularity_transfers.csv.gz");
            var blobA = await GetBlobAsync(Options.Value.PopularityTransferContainerName, "latest_popularity_transfers.csv.gz");
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(1);
            await AssertCsvBlobAsync(PopularityTransfersToCsvDir, Step1, "latest_popularity_transfers.csv.gz");
            var blobB = await GetBlobAsync(Options.Value.PopularityTransferContainerName, "latest_popularity_transfers.csv.gz");
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.Equal(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task PopularityTransfersToCsv_DifferentVersionSet()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PopularityTransfer>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(PopularityTransfersToCsvDir, Step1, "latest_popularity_transfers.csv.gz");
            var blobA = await GetBlobAsync(Options.Value.PopularityTransferContainerName, "latest_popularity_transfers.csv.gz");
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());
            MockVersionSet.Setup(x => x.CommitTimestamp).Returns(new DateTimeOffset(2021, 5, 10, 12, 15, 30, TimeSpan.Zero));

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(1);
            await AssertCsvBlobAsync(PopularityTransfersToCsvDir, Step1, "latest_popularity_transfers.csv.gz");
            var blobB = await GetBlobAsync(Options.Value.PopularityTransferContainerName, "latest_popularity_transfers.csv.gz");
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.NotEqual(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task PopularityTransfersToCsv_NonExistentId()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PopularityTransfer>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            string id;
            MockVersionSet.Setup(x => x.TryGetId("WindowsAzure.Storage", out id)).Returns(false);

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(PopularityTransfersToCsv_NonExistentIdDir, Step1, "popularity_transfers_08585907868854775807.csv.gz");
            await AssertCsvBlobAsync(PopularityTransfersToCsv_NonExistentIdDir, Step1, "latest_popularity_transfers.csv.gz");

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());
            MockVersionSet
                .Setup(x => x.TryGetId("WindowsAzure.Storage", out id))
                .Returns(true)
                .Callback(new TryGetId((string id, out string outId) => outId = "windowsAZURE.StorAGE"));

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(3);
            await AssertCsvBlobAsync(PopularityTransfersToCsv_NonExistentIdDir, Step1, "popularity_transfers_08585907868854775807.csv.gz");
            await AssertCsvBlobAsync(PopularityTransfersToCsv_NonExistentIdDir, Step2, "popularity_transfers_08585906968854775807.csv.gz");
            await AssertCsvBlobAsync(PopularityTransfersToCsv_NonExistentIdDir, Step2, "latest_popularity_transfers.csv.gz");
        }

        [Fact]
        public async Task PopularityTransfersToCsv_UncheckedId()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;

            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PopularityTransfer>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            MockVersionSet.Setup(x => x.GetUncheckedIds()).Returns(new[] { "UncheckedB", "UncheckedA" });

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(PopularityTransfersToCsv_UncheckedIdDir, Step1, "popularity_transfers_08585907868854775807.csv.gz");
            await AssertCsvBlobAsync(PopularityTransfersToCsv_UncheckedIdDir, Step1, "latest_popularity_transfers.csv.gz");
        }

        [Fact]
        public async Task PopularityTransfersToCsv_JustLatest()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PopularityTransfer>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(PopularityTransfersToCsvDir, Step1, "latest_popularity_transfers.csv.gz");

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(1);
            await AssertCsvBlobAsync(PopularityTransfersToCsvDir, Step2, "latest_popularity_transfers.csv.gz");
        }

        private async Task ProcessQueueAsync(IAuxiliaryFileUpdaterService<AsOfData<PopularityTransfer>> service)
        {
            await ProcessQueueAsync(async () => !await service.IsRunningAsync());
        }

        private void Configure()
        {
            ConfigureSettings = x => x.PopularityTransfersV1Urls = new List<string> { $"http://localhost/{TestInput}/{PopularityTransfersToCsvDir}/popularity-transfers.v1.json" };
            SetData(Step1);
        }

        private void SetData(string stepName)
        {
            HttpMessageHandlerFactory.OnSendAsync = async (req, _, _) =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/popularity-transfers.v1.json", StringComparison.Ordinal))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestInput}/{PopularityTransfersToCsvDir}/{stepName}/popularity-transfers.v1.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };
        }

        protected async Task AssertCsvCountAsync(int expected)
        {
            await AssertBlobCountAsync(Options.Value.PopularityTransferContainerName, expected);
        }

        private Task AssertCsvBlobAsync(string testName, string stepName, string blobName)
        {
            return AssertCsvAsync<PopularityTransfersRecord>(Options.Value.PopularityTransferContainerName, testName, stepName, "latest_popularity_transfers.csv", blobName);
        }

        public PopularityTransfersToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            SetupDefaultMockVersionSet();
        }

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddTransient(s => MockVersionSetProvider.Object);
            });
        }
    }
}

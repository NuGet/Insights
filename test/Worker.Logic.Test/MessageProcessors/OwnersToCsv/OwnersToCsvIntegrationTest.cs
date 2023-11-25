// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;

namespace NuGet.Insights.Worker.OwnersToCsv
{
    public class OwnersToCsvIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public const string OwnersToCsvDir = nameof(OwnersToCsv);
        private const string OwnersToCsv_NonExistentIdDir = nameof(OwnersToCsvDir_NonExistentId);
        private const string OwnersToCsv_UncheckedIdDir = nameof(OwnersToCsvDir_UncheckedId);

        [Fact]
        public async Task OwnersToCsv()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "owners_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "latest_owners.csv.gz");

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertBlobCountAsync(Options.Value.PackageOwnerContainerName, 3);
            await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "owners_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(OwnersToCsvDir, Step2, "owners_08585908696854775807.csv.gz");
            await AssertCsvBlobAsync(OwnersToCsvDir, Step2, "latest_owners.csv.gz");
        }

        [Fact]
        public async Task OwnersToCsv_NoOp()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "latest_owners.csv.gz");
            var blobA = await GetBlobAsync(Options.Value.PackageOwnerContainerName, "latest_owners.csv.gz");
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertBlobCountAsync(Options.Value.PackageOwnerContainerName, 1);
            await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "latest_owners.csv.gz");
            var blobB = await GetBlobAsync(Options.Value.PackageOwnerContainerName, "latest_owners.csv.gz");
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.Equal(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task OwnersToCsv_DifferentVersionSet()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "latest_owners.csv.gz");
            var blobA = await GetBlobAsync(Options.Value.PackageOwnerContainerName, "latest_owners.csv.gz");
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());
            MockVersionSet.Setup(x => x.CommitTimestamp).Returns(new DateTimeOffset(2021, 5, 10, 12, 15, 30, TimeSpan.Zero));

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertBlobCountAsync(Options.Value.PackageOwnerContainerName, 1);
            await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "latest_owners.csv.gz");
            var blobB = await GetBlobAsync(Options.Value.PackageOwnerContainerName, "latest_owners.csv.gz");
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.NotEqual(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task OwnersToCsvDir_NonExistentId()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            string id;
            MockVersionSet.Setup(x => x.TryGetId("Knapcode.TorSharp", out id)).Returns(false);
            MockVersionSet.Setup(x => x.TryGetId("Newtonsoft.Json", out id)).Returns(false);

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(OwnersToCsv_NonExistentIdDir, Step1, "owners_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(OwnersToCsv_NonExistentIdDir, Step1, "latest_owners.csv.gz");

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());
            MockVersionSet
                .Setup(x => x.TryGetId("Knapcode.TorSharp", out id))
                .Returns(true)
                .Callback(new TryGetId((string id, out string outId) => outId = "knapcode.TORSHARP"));

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertBlobCountAsync(Options.Value.PackageOwnerContainerName, 3);
            await AssertCsvBlobAsync(OwnersToCsv_NonExistentIdDir, Step1, "owners_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(OwnersToCsv_NonExistentIdDir, Step2, "owners_08585908696854775807.csv.gz");
            await AssertCsvBlobAsync(OwnersToCsv_NonExistentIdDir, Step2, "latest_owners.csv.gz");
        }

        [Fact]
        public async Task OwnersToCsvDir_UncheckedId()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;

            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            MockVersionSet.Setup(x => x.GetUncheckedIds()).Returns(new[] { "UncheckedB", "UncheckedA" });

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(OwnersToCsv_UncheckedIdDir, Step1, "owners_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(OwnersToCsv_UncheckedIdDir, Step1, "latest_owners.csv.gz");
        }

        [Fact]
        public async Task OwnersToCsv_JustLatest()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "latest_owners.csv.gz");

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertBlobCountAsync(Options.Value.PackageOwnerContainerName, 1);
            await AssertCsvBlobAsync(OwnersToCsvDir, Step2, "latest_owners.csv.gz");
        }

        private async Task ProcessQueueAsync(IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>> service)
        {
            await ProcessQueueAsync(async () => !await service.IsRunningAsync());
        }

        private void Configure()
        {
            ConfigureSettings = x => x.OwnersV2Urls = new List<string> { $"http://localhost/{TestInput}/{OwnersToCsvDir}/owners.v2.json" };
            SetData(Step1);
        }

        private void SetData(string stepName)
        {
            HttpMessageHandlerFactory.OnSendAsync = async (req, _, _) =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/owners.v2.json", StringComparison.Ordinal))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestInput}/{OwnersToCsvDir}/{stepName}/owners.v2.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };
        }

        private Task AssertCsvBlobAsync(string testName, string stepName, string blobName)
        {
            return AssertCsvBlobAsync<PackageOwnerRecord>(Options.Value.PackageOwnerContainerName, testName, stepName, "latest_owners.csv", blobName);
        }

        public OwnersToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
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

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;

namespace NuGet.Insights.Worker.OwnersToCsv
{
    public class OwnersToCsvIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public const string OwnersToCsvDir = nameof(OwnersToCsv);
        private const string OwnersToCsv_NonExistentIdDir = nameof(OwnersToCsv_NonExistentId);
        private const string OwnersToCsv_UncheckedIdDir = nameof(OwnersToCsv_UncheckedId);

        [Fact]
        public async Task OwnersToCsv()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageOwnerRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(OwnersToCsvDir, Step1);

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(OwnersToCsvDir, Step2);
        }

        [Fact]
        public async Task OwnersToCsv_NoOp()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageOwnerRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(OwnersToCsvDir, Step1);
            var blobA = await GetBlobAsync(Options.Value.PackageOwnerContainerName, 0);
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(OwnersToCsvDir, Step1);
            var blobB = await GetBlobAsync(Options.Value.PackageOwnerContainerName, 0);
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.Equal(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task OwnersToCsv_DifferentVersionSet()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageOwnerRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(OwnersToCsvDir, Step1);
            var blobA = await GetBlobAsync(Options.Value.PackageOwnerContainerName, 0);
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());
            MockVersionSet.Setup(x => x.CommitTimestamp).Returns(new DateTimeOffset(2021, 5, 10, 12, 15, 30, TimeSpan.Zero));

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(OwnersToCsvDir, Step1);
            var blobB = await GetBlobAsync(Options.Value.PackageOwnerContainerName, 0);
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.NotEqual(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task OwnersToCsv_NonExistentId()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageOwnerRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            string id;
            MockVersionSet.Setup(x => x.TryGetId("Knapcode.TorSharp", out id)).Returns(false);
            MockVersionSet.Setup(x => x.TryGetId("Newtonsoft.Json", out id)).Returns(false);

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(OwnersToCsv_NonExistentIdDir, Step1);

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
            await AssertCsvBlobAsync(OwnersToCsv_NonExistentIdDir, Step2);
        }

        [Fact]
        public async Task OwnersToCsv_UncheckedId()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageOwnerRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            MockVersionSet.Setup(x => x.GetUncheckedIds()).Returns(new[] { "UncheckedB", "UncheckedA" });

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(OwnersToCsv_UncheckedIdDir, Step1);
        }

        private async Task ProcessQueueAsync(IAuxiliaryFileUpdaterService<PackageOwnerRecord> service)
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

        private Task AssertCsvBlobAsync(string testName, string stepName)
        {
            return AssertCsvAsync<PackageOwnerRecord>(Options.Value.PackageOwnerContainerName, testName, stepName, 0);
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
                serviceCollection.AddSingleton(s => MockVersionSetProvider.Object);
            });
        }
    }
}

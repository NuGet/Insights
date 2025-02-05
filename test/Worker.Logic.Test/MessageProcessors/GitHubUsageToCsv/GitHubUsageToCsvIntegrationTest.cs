// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;

namespace NuGet.Insights.Worker.GitHubUsageToCsv
{
    public class GitHubUsageToCsvIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public const string GitHubUsageToCsvDir = nameof(GitHubUsageToCsv);
        private const string GitHubUsageToCsv_NonExistentIdDir = nameof(GitHubUsageToCsv_NonExistentId);
        private const string GitHubUsageToCsv_UncheckedIdDir = nameof(GitHubUsageToCsv_UncheckedId);

        [Fact]
        public async Task GitHubUsageToCsv()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<GitHubUsageRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(GitHubUsageToCsvDir, Step1);

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(GitHubUsageToCsvDir, Step2);
        }

        [Fact]
        public async Task GitHubUsageToCsv_NoOp()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<GitHubUsageRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(GitHubUsageToCsvDir, Step1);
            var blobA = await GetBlobAsync(Options.Value.GitHubUsageContainerName, 0);
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(GitHubUsageToCsvDir, Step1);
            var blobB = await GetBlobAsync(Options.Value.GitHubUsageContainerName, 0);
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.Equal(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task GitHubUsageToCsv_DifferentVersionSet()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<GitHubUsageRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(GitHubUsageToCsvDir, Step1);
            var blobA = await GetBlobAsync(Options.Value.GitHubUsageContainerName, 0);
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());
            MockVersionSet.Setup(x => x.CommitTimestamp).Returns(new DateTimeOffset(2021, 5, 10, 12, 15, 30, TimeSpan.Zero));

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(GitHubUsageToCsvDir, Step1);
            var blobB = await GetBlobAsync(Options.Value.GitHubUsageContainerName, 0);
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.NotEqual(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task GitHubUsageToCsv_NonExistentId()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<GitHubUsageRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            string id;
            MockVersionSet.Setup(x => x.TryGetId("WindowsAzure.Storage", out id)).Returns(false);

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(GitHubUsageToCsv_NonExistentIdDir, Step1);

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
            await AssertCsvBlobAsync(GitHubUsageToCsv_NonExistentIdDir, Step2);
        }

        [Fact]
        public async Task GitHubUsageToCsv_UncheckedId()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<GitHubUsageRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            MockVersionSet.Setup(x => x.GetUncheckedIds()).Returns(new[] { "UncheckedB", "UncheckedA" });

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(GitHubUsageToCsv_UncheckedIdDir, Step1);
        }

        private async Task ProcessQueueAsync(IAuxiliaryFileUpdaterService<GitHubUsageRecord> service)
        {
            await ProcessQueueAsync(async () => !await service.IsRunningAsync());
        }

        private void Configure()
        {
            ConfigureSettings = x => x.GitHubUsageV1Urls = new List<string> { $"http://localhost/{TestInput}/{GitHubUsageToCsvDir}/GitHubUsage.v1.json" };
            SetData(Step1);
        }

        private void SetData(string stepName)
        {
            HttpMessageHandlerFactory.OnSendAsync = async (req, _, _) =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/GitHubUsage.v1.json", StringComparison.Ordinal))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestInput}/{GitHubUsageToCsvDir}/{stepName}/GitHubUsage.v1.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };
        }

        private Task AssertCsvBlobAsync(string testName, string stepName)
        {
            return AssertCsvAsync<GitHubUsageRecord>(Options.Value.GitHubUsageContainerName, testName, stepName, 0);
        }

        public GitHubUsageToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
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

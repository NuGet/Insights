using System;
using System.IO;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.StreamWriterUpdater;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.OwnersToCsv
{
    public class OwnersToCsvIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        private const string OwnersToCsvDir = nameof(Worker.OwnersToCsv);
        private const string OwnersToCsvDir_NonExistentIdDir = nameof(OwnersToCsvDir_NonExistentId);

        public OwnersToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            MockVersionSet.SetReturnsDefault(true);
        }

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddTransient(s => MockVersionSetProvider.Object);
            });
        }

        public class OwnersToCsv : OwnersToCsvIntegrationTest
        {
            public OwnersToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task ExecuteAsync()
            {
                // Arrange
                ConfigureWorkerSettings = x => x.OnlyKeepLatestInStreamWriterUpdater = false;
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IStreamWriterUpdaterService<PackageOwnerSet>>();
                await service.InitializeAsync();
                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "owners_08585909596854775807.csv.gz");
                await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "latest_owners.csv.gz");

                // Arrange
                SetData(Step2);
                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageOwnersContainerName, 3);
                await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "owners_08585909596854775807.csv.gz");
                await AssertCsvBlobAsync(OwnersToCsvDir, Step2, "owners_08585908696854775807.csv.gz");
                await AssertCsvBlobAsync(OwnersToCsvDir, Step2, "latest_owners.csv.gz");
            }
        }

        public class OwnersToCsvDir_NonExistentId : OwnersToCsvIntegrationTest
        {
            public OwnersToCsvDir_NonExistentId(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task ExecuteAsync()
            {
                // Arrange
                ConfigureWorkerSettings = x => x.OnlyKeepLatestInStreamWriterUpdater = false;
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IStreamWriterUpdaterService<PackageOwnerSet>>();
                await service.InitializeAsync();
                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);
                MockVersionSet.Setup(x => x.DidIdEverExist("Knapcode.TorSharp")).Returns(false);
                MockVersionSet.Setup(x => x.DidIdEverExist("Newtonsoft.Json")).Returns(false);

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(OwnersToCsvDir_NonExistentIdDir, Step1, "owners_08585909596854775807.csv.gz");
                await AssertCsvBlobAsync(OwnersToCsvDir_NonExistentIdDir, Step1, "latest_owners.csv.gz");

                // Arrange
                SetData(Step2);
                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);
                MockVersionSet.Setup(x => x.DidIdEverExist("Knapcode.TorSharp")).Returns(true);

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageOwnersContainerName, 3);
                await AssertCsvBlobAsync(OwnersToCsvDir_NonExistentIdDir, Step1, "owners_08585909596854775807.csv.gz");
                await AssertCsvBlobAsync(OwnersToCsvDir_NonExistentIdDir, Step2, "owners_08585908696854775807.csv.gz");
                await AssertCsvBlobAsync(OwnersToCsvDir_NonExistentIdDir, Step2, "latest_owners.csv.gz");
            }
        }

        public class OwnersToCsv_JustLatest : OwnersToCsvIntegrationTest
        {
            public OwnersToCsv_JustLatest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task ExecuteAsync()
            {
                // Arrange
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IStreamWriterUpdaterService<PackageOwnerSet>>();
                await service.InitializeAsync();
                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "latest_owners.csv.gz");

                // Arrange
                SetData(Step2);
                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageOwnersContainerName, 1);
                await AssertCsvBlobAsync(OwnersToCsvDir, Step2, "latest_owners.csv.gz");
            }
        }

        private async Task ProcessQueueAsync(IStreamWriterUpdaterService<PackageOwnerSet> service)
        {
            await ProcessQueueAsync(() => { }, async () => !await service.IsRunningAsync());
        }

        private void ConfigureAndSetLastModified()
        {
            ConfigureSettings = x => x.OwnersV2Url = $"http://localhost/{TestData}/{OwnersToCsvDir}/owners.v2.json";

            // Set the Last-Modified date
            var fileA = new FileInfo(Path.Combine(TestData, OwnersToCsvDir, Step1, "owners.v2.json"))
            {
                LastWriteTimeUtc = DateTime.Parse("2021-01-14T18:00:00Z")
            };
            var fileB = new FileInfo(Path.Combine(TestData, OwnersToCsvDir, Step2, "owners.v2.json"))
            {
                LastWriteTimeUtc = DateTime.Parse("2021-01-15T19:00:00Z")
            };

            SetData(Step1);
        }

        private void SetData(string stepName)
        {
            HttpMessageHandlerFactory.OnSendAsync = async req =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/owners.v2.json"))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestData}/{OwnersToCsvDir}/{stepName}/owners.v2.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };
        }

        private Task AssertCsvBlobAsync(string testName, string stepName, string blobName)
        {
            return AssertCsvBlobAsync<PackageOwnerRecord>(Options.Value.PackageOwnersContainerName, testName, stepName, blobName);
        }
    }
}

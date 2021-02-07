using System;
using System.IO;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.StreamWriterUpdater;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.OwnersToCsv
{
    public class OwnersToCsvIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        private const string OwnersToCsvDir = nameof(Worker.OwnersToCsv);

        public OwnersToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
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
                ConfigureSettings = x => x.OwnersV2Url = $"http://localhost/{TestData}/{OwnersToCsvDir}/owners.v2.json";
                ConfigureWorkerSettings = x => x.OnlyKeepLatestInStreamWriterUpdater = false;

                // Set the Last-Modified date
                var fileA = new FileInfo(Path.Combine(TestData, OwnersToCsvDir, Step1, "owners.v2.json"))
                {
                    LastWriteTimeUtc = DateTime.Parse("2021-01-14T18:00:00Z")
                };
                var fileB = new FileInfo(Path.Combine(TestData, OwnersToCsvDir, Step2, "owners.v2.json"))
                {
                    LastWriteTimeUtc = DateTime.Parse("2021-01-15T19:00:00Z")
                };

                var service = Host.Services.GetRequiredService<IStreamWriterUpdaterService<PackageOwnerSet>>();
                await service.InitializeAsync();

                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsolutePath.EndsWith("/owners.v2.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/{OwnersToCsvDir}/{Step1}/owners.v2.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };

                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);

                // Act
                await ProcessQueueAsync(() => { }, async () => !await service.IsRunningAsync());

                // Assert
                await AssertBlobAsync(Options.Value.PackageOwnersContainerName, OwnersToCsvDir, Step1, "owners_08585909596854775807.csv.gz", gzip: true);
                await AssertBlobAsync(Options.Value.PackageOwnersContainerName, OwnersToCsvDir, Step1, "latest_owners.csv.gz", gzip: true);

                // Arrange
                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsolutePath.EndsWith("/owners.v2.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/{OwnersToCsvDir}/{Step2}/owners.v2.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };

                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);

                // Act
                await ProcessQueueAsync(() => { }, async () => !await service.IsRunningAsync());

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageOwnersContainerName, 3);
                await AssertBlobAsync(Options.Value.PackageOwnersContainerName, OwnersToCsvDir, Step1, "owners_08585909596854775807.csv.gz", gzip: true);
                await AssertBlobAsync(Options.Value.PackageOwnersContainerName, OwnersToCsvDir, Step2, "owners_08585908696854775807.csv.gz", gzip: true);
                await AssertBlobAsync(Options.Value.PackageOwnersContainerName, OwnersToCsvDir, Step2, "latest_owners.csv.gz", gzip: true);
                AssertOnlyInfoLogsOrLess();
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

                var service = Host.Services.GetRequiredService<IStreamWriterUpdaterService<PackageOwnerSet>>();
                await service.InitializeAsync();

                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsolutePath.EndsWith("/owners.v2.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/{OwnersToCsvDir}/{Step1}/owners.v2.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };

                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);

                // Act
                await ProcessQueueAsync(() => { }, async () => !await service.IsRunningAsync());

                // Assert
                await AssertBlobAsync(Options.Value.PackageOwnersContainerName, OwnersToCsvDir, Step1, "latest_owners.csv.gz", gzip: true);

                // Arrange
                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsolutePath.EndsWith("/owners.v2.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/{OwnersToCsvDir}/{Step2}/owners.v2.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };

                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);

                // Act
                await ProcessQueueAsync(() => { }, async () => !await service.IsRunningAsync());

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageOwnersContainerName, 1);
                await AssertBlobAsync(Options.Value.PackageOwnersContainerName, OwnersToCsvDir, Step2, "latest_owners.csv.gz", gzip: true);
                AssertOnlyInfoLogsOrLess();
            }
        }
    }
}

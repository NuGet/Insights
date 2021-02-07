using System;
using System.IO;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.StreamWriterUpdater;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.DownloadsToCsv
{
    public class DownloadsToCsvIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        private const string DownloadsToCsvDir = nameof(DownloadsToCsv);

        public DownloadsToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public class DownloadsToCsv : DownloadsToCsvIntegrationTest
        {
            public DownloadsToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task ExecuteAsync()
            {
                // Arrange
                ConfigureSettings = x => x.DownloadsV1Url = $"http://localhost/{TestData}/{DownloadsToCsvDir}/downloads.v1.json";
                ConfigureWorkerSettings = x => x.OnlyKeepLatestInStreamWriterUpdater = false;

                // Set the Last-Modified date
                var fileA = new FileInfo(Path.Combine(TestData, DownloadsToCsvDir, Step1, "downloads.v1.json"))
                {
                    LastWriteTimeUtc = DateTime.Parse("2021-01-14T18:00:00Z")
                };
                var fileB = new FileInfo(Path.Combine(TestData, DownloadsToCsvDir, Step2, "downloads.v1.json"))
                {
                    LastWriteTimeUtc = DateTime.Parse("2021-01-15T19:00:00Z")
                };

                var service = Host.Services.GetRequiredService<IStreamWriterUpdaterService<PackageDownloadSet>>();
                await service.InitializeAsync();

                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsolutePath.EndsWith("/downloads.v1.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/{DownloadsToCsvDir}/{Step1}/downloads.v1.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };

                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);

                // Act
                await ProcessQueueAsync(() => { }, async () => !await service.IsRunningAsync());

                // Assert
                await AssertBlobAsync(Options.Value.PackageDownloadsContainerName, DownloadsToCsvDir, Step1, "downloads_08585909596854775807.csv.gz", gzip: true);
                await AssertBlobAsync(Options.Value.PackageDownloadsContainerName, DownloadsToCsvDir, Step1, "latest_downloads.csv.gz", gzip: true);

                // Arrange
                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsolutePath.EndsWith("/downloads.v1.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/{DownloadsToCsvDir}/{Step2}/downloads.v1.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };

                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);

                // Act
                await ProcessQueueAsync(() => { }, async () => !await service.IsRunningAsync());

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageDownloadsContainerName, 3);
                await AssertBlobAsync(Options.Value.PackageDownloadsContainerName, DownloadsToCsvDir, Step1, "downloads_08585909596854775807.csv.gz", gzip: true);
                await AssertBlobAsync(Options.Value.PackageDownloadsContainerName, DownloadsToCsvDir, Step2, "downloads_08585908696854775807.csv.gz", gzip: true);
                await AssertBlobAsync(Options.Value.PackageDownloadsContainerName, DownloadsToCsvDir, Step2, "latest_downloads.csv.gz", gzip: true);
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class DownloadsToCsv_JustLatest : DownloadsToCsvIntegrationTest
        {
            public DownloadsToCsv_JustLatest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task ExecuteAsync()
            {
                // Arrange
                ConfigureSettings = x => x.DownloadsV1Url = $"http://localhost/{TestData}/{DownloadsToCsvDir}/downloads.v1.json";

                // Set the Last-Modified date
                var fileA = new FileInfo(Path.Combine(TestData, DownloadsToCsvDir, Step1, "downloads.v1.json"))
                {
                    LastWriteTimeUtc = DateTime.Parse("2021-01-14T18:00:00Z")
                };
                var fileB = new FileInfo(Path.Combine(TestData, DownloadsToCsvDir, Step2, "downloads.v1.json"))
                {
                    LastWriteTimeUtc = DateTime.Parse("2021-01-15T19:00:00Z")
                };

                var service = Host.Services.GetRequiredService<IStreamWriterUpdaterService<PackageDownloadSet>>();
                await service.InitializeAsync();

                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsolutePath.EndsWith("/downloads.v1.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/{DownloadsToCsvDir}/{Step1}/downloads.v1.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };

                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);

                // Act
                await ProcessQueueAsync(() => { }, async () => !await service.IsRunningAsync());

                // Assert
                await AssertBlobAsync(Options.Value.PackageDownloadsContainerName, DownloadsToCsvDir, Step1, "latest_downloads.csv.gz", gzip: true);

                // Arrange
                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsolutePath.EndsWith("/downloads.v1.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/{DownloadsToCsvDir}/{Step2}/downloads.v1.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };

                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);

                // Act
                await ProcessQueueAsync(() => { }, async () => !await service.IsRunningAsync());

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageDownloadsContainerName, 1);
                await AssertBlobAsync(Options.Value.PackageDownloadsContainerName, DownloadsToCsvDir, Step2, "latest_downloads.csv.gz", gzip: true);
                AssertOnlyInfoLogsOrLess();
            }
        }
    }
}

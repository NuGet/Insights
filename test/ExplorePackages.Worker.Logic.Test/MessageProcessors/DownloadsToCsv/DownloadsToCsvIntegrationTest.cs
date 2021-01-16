using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
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
                var downloadsV1Url = $"http://localhost/{TestData}/{DownloadsToCsvDir}/downloads.v1.json";
                ConfigureSettings = x => x.DownloadsV1Url = downloadsV1Url;
                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsolutePath.EndsWith("/downloads.v1.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri(downloadsV1Url);
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };

                // Set the Last-Modified date
                var downloadsFile = new FileInfo(Path.Combine(TestData, DownloadsToCsvDir, "downloads.v1.json"));
                downloadsFile.LastWriteTimeUtc = DateTime.Parse("2021-01-14T18:00:00Z");

                var service = Host.Services.GetRequiredService<DownloadsToCsvService>();
                await service.InitializeAsync();

                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);

                // Act
                await ProcessQueueAsync(() => { }, async () => !await service.IsRunningAsync());

                // Assert
                await AssertBlobAsync(Options.Value.PackageDownloadsContainerName, DownloadsToCsvDir, Step1, "downloads_08585909596854775807.csv.gz", gzip: true);
            }
        }
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
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
                var ownersV2Url = $"http://localhost/{TestData}/{OwnersToCsvDir}/owners.v2.json";
                ConfigureSettings = x => x.OwnersV2Url = ownersV2Url;
                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsolutePath.EndsWith("/owners.v2.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri(ownersV2Url);
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };

                // Set the Last-Modified date
                var file = new FileInfo(Path.Combine(TestData, OwnersToCsvDir, "owners.v2.json"))
                {
                    LastWriteTimeUtc = DateTime.Parse("2021-01-14T18:00:00Z")
                };

                var service = Host.Services.GetRequiredService<OwnersToCsvService>();
                await service.InitializeAsync();

                await service.StartAsync(loop: false, notBefore: TimeSpan.Zero);

                // Act
                await ProcessQueueAsync(() => { }, async () => !await service.IsRunningAsync());

                // Assert
                await AssertBlobAsync(Options.Value.PackageOwnersContainerName, OwnersToCsvDir, Step1, "owners_08585909596854775807.csv.gz", gzip: true);
                AssertOnlyInfoLogsOrLess();
            }
        }
    }
}

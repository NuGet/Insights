// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;

namespace NuGet.Insights.Worker
{
    public class CanAllRunTimers : EndToEndTest
    {
        public CanAllRunTimers(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
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

        [Fact]
        public async Task Execute()
        {
            // Arrange
            ConfigureSettings = x =>
            {
                x.DownloadsV1Urls = new List<string> { $"http://localhost/{TestInput}/DownloadsToCsv/{Step1}/downloads.v1.json" };
                x.OwnersV2Urls = new List<string> { $"http://localhost/{TestInput}/OwnersToCsv/{Step1}/owners.v2.json" };
                x.VerifiedPackagesV1Urls = new List<string> { $"http://localhost/{TestInput}/VerifiedPackagesToCsv/{Step1}/verifiedPackages.json" };
            };
            ConfigureWorkerSettings = x =>
            {
                x.AutoStartDownloadToCsv = true;
                x.AutoStartOwnersToCsv = true;
                x.AutoStartVerifiedPackagesToCsv = true;
            };

            HttpMessageHandlerFactory.OnSendAsync = async (req, _, _) =>
            {
                if (Options.Value.DownloadsV1Urls.Contains(req.RequestUri.AbsoluteUri)
                    || Options.Value.OwnersV2Urls.Contains(req.RequestUri.AbsoluteUri)
                    || Options.Value.VerifiedPackagesV1Urls.Contains(req.RequestUri.AbsoluteUri))
                {
                    return await TestDataHttpClient.SendAsync(Clone(req));
                }

                return null;
            };

            var service = Host.Services.GetRequiredService<TimerExecutionService>();
            await service.InitializeAsync();

            // Act
            using (var scope = Host.Services.CreateScope())
            {
                await scope
                    .ServiceProvider
                    .GetRequiredService<Functions>()
                    .TimerAsync(timerInfo: null);
            }

            await ProcessQueueAsync(async () => (await service.GetStateAsync()).All(x => !x.IsRunning));

            // Assert
            await AssertBlobCountAsync(Options.Value.PackageDownloadContainerName, 1);
            await AssertBlobCountAsync(Options.Value.PackageOwnerContainerName, 1);
            await AssertBlobCountAsync(Options.Value.VerifiedPackageContainerName, 1);
        }
    }
}

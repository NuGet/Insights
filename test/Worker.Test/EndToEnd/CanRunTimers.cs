// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Worker
{
    public class CanRunTimers : EndToEndTest
    {
        public CanRunTimers(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
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

        /// <summary>
        /// We only run timers here that can be pretty fast.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Execute()
        {
            // Arrange
            ConfigureSettings = x => ConfigureAllAuxiliaryFiles(x);

            HttpMessageHandlerFactory.OnSendAsync = async (req, _, _) =>
            {
                if (Options.Value.DownloadsV1Urls.Contains(req.RequestUri.AbsoluteUri)
                    || Options.Value.OwnersV2Urls.Contains(req.RequestUri.AbsoluteUri)
                    || Options.Value.VerifiedPackagesV1Urls.Contains(req.RequestUri.AbsoluteUri)
                    || Options.Value.ExcludedPackagesV1Urls.Contains(req.RequestUri.AbsoluteUri)
                    || Options.Value.PopularityTransfersV1Urls.Contains(req.RequestUri.AbsoluteUri)
                    || Options.Value.GitHubUsageV1Urls.Contains(req.RequestUri.AbsoluteUri))
                {
                    return await TestDataHttpClient.SendAsync(Clone(req));
                }

                return null;
            };

            var excludedTimerTypes = new[] { typeof(WorkflowTimer), typeof(CatalogScanUpdateTimer), typeof(KustoIngestionTimer) };
            var timers = Host.Services.GetServices<ITimer>().ToList();
            var excludedTimers = timers.Where(x => excludedTimerTypes.Contains(x.GetType())).ToList();
            var includedTimers = timers.Where(x => !excludedTimerTypes.Contains(x.GetType())).ToList();
            var service = Host.Services.GetRequiredService<TimerExecutionService>();
            await service.InitializeAsync();
            foreach (var timer in includedTimers)
            {
                await service.SetIsEnabledAsync(timer.Name, isEnabled: true);
            }

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
            var timerState = await service.GetStateAsync();

            var executedTimerState = timerState.Where(x => x.LastExecuted.HasValue).ToList();
            Assert.All(includedTimers, t => Assert.Contains(t.Name, executedTimerState.Select(x => x.Name)));
            Assert.Equal(executedTimerState.Count, includedTimers.Count);

            var skippedTimerState = timerState.Where(x => !x.LastExecuted.HasValue).ToList();
            Assert.All(excludedTimers, t => Assert.Contains(t.Name, skippedTimerState.Select(x => x.Name)));
            Assert.Equal(skippedTimerState.Count, excludedTimers.Count);

            var auxiliaryTimers = timers.Select(x => x as IAuxiliaryFileUpdaterTimer).Where(x => x is not null).ToList();
            Assert.All(auxiliaryTimers, t => Assert.True(t.IsEnabled));
            foreach (var timer in auxiliaryTimers)
            {
                await AssertBlobCountAsync(timer.ContainerName, 1);
            }
        }
    }
}

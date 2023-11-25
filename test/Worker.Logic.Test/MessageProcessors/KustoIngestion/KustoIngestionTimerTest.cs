// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoIngestionTimerTest : BaseWorkerLogicIntegrationTest
    {
        public KustoIngestionTimerTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddTransient<KustoIngestionTimer>();
            });
        }

        public KustoIngestionTimer Target => Host.Services.GetRequiredService<KustoIngestionTimer>();

        [Fact]
        public async Task StartsKustoIngestionAsync()
        {
            await Target.InitializeAsync();

            var result = await Target.ExecuteAsync();

            var ingestions = await KustoIngestionStorageService.GetIngestionsAsync();
            Assert.Single(ingestions);
            Assert.True(result);
        }

        [Fact]
        public async Task DoesNotStartKustoIngestionWhenIngestionIsAlreadyRunningAsync()
        {
            await Target.InitializeAsync();
            var initialRun = await KustoIngestionService.StartAsync();

            var result = await Target.ExecuteAsync();

            Assert.NotNull(initialRun);
            var ingestions = await KustoIngestionStorageService.GetIngestionsAsync();
            Assert.Single(ingestions);
            Assert.False(result);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;

namespace NuGet.Insights.Worker
{
    public class CatalogScanUpdateTimerTest : BaseWorkerLogicIntegrationTest
    {
        public CatalogScanUpdateTimerTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddTransient<CatalogScanUpdateTimer>();
            });
        }

        public CatalogScanUpdateTimer Target => Host.Services.GetRequiredService<CatalogScanUpdateTimer>();

        [Fact]
        public async Task StartsCatalogScansAsync()
        {
            await Target.InitializeAsync();

            var result = await Target.ExecuteAsync();

            var catalogScans = await CatalogScanStorageService.GetIndexScansAsync();
            Assert.NotEmpty(catalogScans);
            Assert.True(result);
        }

        [Fact]
        public async Task DoesNotStartCatalogScansWhenScansAreAlreadyRunningAsync()
        {
            AssertLogLevel = LogLevel.Error;

            await Target.InitializeAsync();
            await CatalogScanService.UpdateAllAsync(max: null);

            var result = await Target.ExecuteAsync();

            var catalogScans = await CatalogScanStorageService.GetIndexScansAsync();
            Assert.NotEmpty(catalogScans);
            Assert.False(result);
        }
    }
}

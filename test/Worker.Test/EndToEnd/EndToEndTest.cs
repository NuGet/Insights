// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Hosting;

namespace NuGet.Insights.Worker
{
    public class EndToEndTest : BaseWorkerLogicIntegrationTest
    {
        public EndToEndTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            hostBuilder
                .ConfigureNuGetInsightsWorker()
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddTransient(s => Output.GetTelemetryClient());
                    serviceCollection.AddTransient<Functions>();

                    serviceCollection.Configure((Action<NuGetInsightsSettings>)AssertDefaultsAndSettings);
                    serviceCollection.Configure((Action<NuGetInsightsWorkerSettings>)AssertWorkerDefaultsAndSettings);
                });
        }

        protected override async Task ProcessMessageAsync(IServiceProvider serviceProvider, QueueType queueType, QueueMessage message)
        {
            var functions = serviceProvider.GetRequiredService<Functions>();
            switch (queueType)
            {
                case QueueType.Work:
                    await functions.WorkQueueAsync(message);
                    break;
                case QueueType.Expand:
                    await functions.ExpandQueueAsync(message);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}

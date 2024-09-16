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
                    serviceCollection.AddSingleton(s => new LoggerTelemetryClient(
                        TestOutputHelperExtensions.ShouldIgnoreMetricLog,
                        s.GetRequiredService<ILogger<LoggerTelemetryClient>>()));
                    serviceCollection.AddSingleton<Functions>();

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

        protected List<(string ContainerName, Type RecordType, string DefaultTableName)> GetExpectedContainers()
        {
            var expectedContainers = new List<(string ContainerName, Type RecordType, string DefaultTableName)>();
            foreach (var recordType in CsvRecordContainers.RecordTypes)
            {
                var producer = CsvRecordContainers.GetProducer(recordType);
                if (producer.Type == CsvRecordProducerType.CatalogScanDriver
                    && !Options.Value.DisabledDrivers.Contains(producer.CatalogScanDriverType.Value))
                {
                    expectedContainers.Add((
                        CsvRecordContainers.GetContainerName(recordType),
                        recordType,
                        CsvRecordContainers.GetDefaultKustoTableName(recordType)));
                }
            }

            return expectedContainers;
        }
    }
}

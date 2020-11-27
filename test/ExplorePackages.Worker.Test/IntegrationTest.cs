using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using System;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Queue;
using System.IO;

namespace Knapcode.ExplorePackages.Worker
{
    public class IntegrationTest
    {
        public IntegrationTest(ITestOutputHelper output)
        {
            var startup = new Startup();
            Host = new HostBuilder()
                .ConfigureWebJobs(startup.Configure)
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddTransient<WorkerQueueFunction>();

                    serviceCollection.AddLogging(o =>
                    {
                        o.SetMinimumLevel(LogLevel.Trace);
                        o.AddProvider(new XunitLoggerProvider(output));
                    });

                    serviceCollection.Configure<ExplorePackagesWorkerSettings>(x =>
                    {
                        x.AppendResultStorageBucketCount = 5;
                        x.AppendResultUniqueIds = false;
                    });
                })
                .Build();

            Options = Host.Services.GetRequiredService<IOptionsSnapshot<ExplorePackagesWorkerSettings>>();
            ServiceClientFactory = Host.Services.GetRequiredService<ServiceClientFactory>();
            WorkerQueueFactory = Host.Services.GetRequiredService<IWorkerQueueFactory>();
            CursorStorageService = Host.Services.GetRequiredService<CursorStorageService>();
            CatalogScanStorageService = Host.Services.GetRequiredService<CatalogScanStorageService>();
            CatalogScanService = Host.Services.GetRequiredService<CatalogScanService>();
            Logger = Host.Services.GetRequiredService<ILogger<IntegrationTest>>();

            Target = Host.Services.GetRequiredService<WorkerQueueFunction>();
        }

        public IHost Host { get; }
        public IOptionsSnapshot<ExplorePackagesWorkerSettings> Options { get; }
        public ServiceClientFactory ServiceClientFactory { get; }
        public IWorkerQueueFactory WorkerQueueFactory { get; }
        public CursorStorageService CursorStorageService { get; }
        public CatalogScanStorageService CatalogScanStorageService { get; }
        public CatalogScanService CatalogScanService { get; }
        public ILogger<IntegrationTest> Logger { get; }
        public WorkerQueueFunction Target { get; }

        [Fact]
        public async Task FindPackageAssets()
        {
            // Arrange
            var min = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
            var max = DateTimeOffset.Parse("2020-11-27T19:41:04.1983255Z");

            var queue = WorkerQueueFactory.GetQueue();
            await queue.CreateAsync();
            await CursorStorageService.InitializeAsync();
            await CatalogScanStorageService.InitializeAsync();

            var cursor = await CursorStorageService.GetOrCreateAsync($"CatalogScan-{CatalogScanType.FindPackageAssets}");
            cursor.Value = min;
            await CursorStorageService.UpdateAsync(cursor);

            var indexScan = await CatalogScanService.UpdateGetPackageAssets(max);

            // Act
            await WaitForIndexScanAsync(indexScan.ScanId);

            // Assert
            var container = ServiceClientFactory
                .GetStorageAccount()
                .CreateCloudBlobClient()
                .GetContainerReference("findpackageassets");
            for (var i = 0; i < Options.Value.AppendResultStorageBucketCount; i++)
            {
                var blob = container.GetBlockBlobReference($"compact_{i}.csv");
                var actual = await blob.DownloadTextAsync();
                var expected = File.ReadAllText(Path.Combine("TestData", "FindPackageAssets", blob.Name));
                Assert.Equal(expected, actual);
            }
        }

        private async Task WaitForIndexScanAsync(string scanId)
        {
            var queue = WorkerQueueFactory.GetQueue();

            CatalogIndexScan indexScan;
            do
            {
                CloudQueueMessage message;
                while ((message = await queue.GetMessageAsync()) != null)
                {
                    try
                    {
                        await Target.ProcessAsync(message.AsString);
                        await queue.DeleteMessageAsync(message);
                    }
                    catch (Exception ex) when (message.DequeueCount < 5)
                    {
                        Logger.LogWarning(ex, "Message with {DequeueCount} dequeues failed with an exception.", message.DequeueCount);
                    }
                }

                indexScan = await CatalogScanStorageService.GetIndexScanAsync(scanId);
            }
            while (indexScan.ParsedState != CatalogScanState.Complete);
        }
    }
}

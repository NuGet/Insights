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
using Newtonsoft.Json;
using System.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class FindPackageAssetsIntegrationTest : IAsyncLifetime
    {
        private const string Step1 = "Step1";
        private const string Step2 = "Step2";

        public FindPackageAssetsIntegrationTest(ITestOutputHelper output)
        {
            StoragePrefix = "t" + StorageUtility.GenerateUniqueId();

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
                        x.AppendResultStorageBucketCount = 3;
                        x.AppendResultUniqueIds = false;

                        x.WorkerQueueName = $"{StoragePrefix}1wq1";
                        x.CursorTableName = $"{StoragePrefix}1c1";
                        x.CatalogIndexScanTableName = $"{StoragePrefix}1cis1";
                        x.CatalogPageScanTableName = $"{StoragePrefix}1cps1";
                        x.CatalogLeafScanTableName = $"{StoragePrefix}1cls1";
                        x.TaskStateTableName = $"{StoragePrefix}1ts1";
                        x.LatestLeavesTableName = $"{StoragePrefix}1ll1";
                        x.FindPackageAssetsContainerName = $"{StoragePrefix}1fpa1";
                        x.RunRealRestoreContainerName = $"{StoragePrefix}1rrr1";
                    });
                })
                .Build();

            Options = Host.Services.GetRequiredService<IOptions<ExplorePackagesWorkerSettings>>();
            ServiceClientFactory = Host.Services.GetRequiredService<ServiceClientFactory>();
            WorkerQueueFactory = Host.Services.GetRequiredService<IWorkerQueueFactory>();
            CursorStorageService = Host.Services.GetRequiredService<CursorStorageService>();
            CatalogScanStorageService = Host.Services.GetRequiredService<CatalogScanStorageService>();
            CatalogScanService = Host.Services.GetRequiredService<CatalogScanService>();
            Logger = Host.Services.GetRequiredService<ILogger<FindPackageAssetsIntegrationTest>>();

            Target = Host.Services.GetRequiredService<WorkerQueueFunction>();
        }

        public string StoragePrefix { get; }
        public IHost Host { get; }
        public IOptions<ExplorePackagesWorkerSettings> Options { get; }
        public ServiceClientFactory ServiceClientFactory { get; }
        public IWorkerQueueFactory WorkerQueueFactory { get; }
        public CursorStorageService CursorStorageService { get; }
        public CatalogScanStorageService CatalogScanStorageService { get; }
        public CatalogScanService CatalogScanService { get; }
        public ILogger<FindPackageAssetsIntegrationTest> Logger { get; }
        public WorkerQueueFunction Target { get; }

        [Fact]
        public async Task FindPackageAssets()
        {
            Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z");
            var cursorName = $"CatalogScan-{CatalogScanType.FindPackageAssets}";

            await WorkerQueueFactory.InitializeAsync();
            await CursorStorageService.InitializeAsync();
            await CatalogScanStorageService.InitializeAsync();

            var cursor = await CursorStorageService.GetOrCreateAsync(cursorName);
            cursor.Value = min0;
            await CursorStorageService.UpdateAsync(cursor);

            // Act
            await UpdateFindPackageAssetsAsync(max1);

            // Assert
            await AssertFindPackageAssetsOutputAsync(Step1, 0);
            await AssertFindPackageAssetsOutputAsync(Step1, 1);
            await AssertFindPackageAssetsOutputAsync(Step1, 2);

            // Act
            await UpdateFindPackageAssetsAsync(max2);

            // Assert
            await AssertFindPackageAssetsOutputAsync(Step2, 0);
            await AssertFindPackageAssetsOutputAsync(Step1, 1); // This file is unchanged.
            await AssertFindPackageAssetsOutputAsync(Step2, 2);

            await VerifyExpectedContainers();
        }

        private async Task VerifyExpectedContainers()
        {
            var account = ServiceClientFactory.GetStorageAccount();

            var containers = await account.CreateCloudBlobClient().ListContainersAsync(StoragePrefix);
            Assert.Equal(
                new[] { Options.Value.FindPackageAssetsContainerName },
                containers.Select(x => x.Name).ToArray());

            var queues = await account.CreateCloudQueueClient().ListQueuesAsync(StoragePrefix);
            Assert.Equal(
                new[] { Options.Value.WorkerQueueName },
                queues.Select(x => x.Name).ToArray());

            var tables = await account.CreateCloudTableClient().ListTablesAsync(StoragePrefix);
            Assert.Equal(
                new[] { Options.Value.CursorTableName, Options.Value.CatalogIndexScanTableName },
                tables.Select(x => x.Name).ToArray());
        }

        private async Task UpdateFindPackageAssetsAsync(DateTimeOffset max)
        {
            var indexScan = await CatalogScanService.UpdateFindPackageAssetsAsync(max);

            var queue = WorkerQueueFactory.GetQueue();
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

                indexScan = await CatalogScanStorageService.GetIndexScanAsync(indexScan.ScanId);

                if (indexScan.ParsedState != CatalogScanState.Complete)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
            while (indexScan.ParsedState != CatalogScanState.Complete);
        }

        private async Task AssertFindPackageAssetsOutputAsync(string stepName, int bucket)
        {
            var client = ServiceClientFactory.GetStorageAccount().CreateCloudBlobClient();
            var container = client.GetContainerReference(Options.Value.FindPackageAssetsContainerName);
            var blob = container.GetBlockBlobReference($"compact_{bucket}.csv");
            var actual = await blob.DownloadTextAsync();
            var expected = File.ReadAllText(Path.Combine("TestData", "FindPackageAssets", stepName, blob.Name));
            Assert.Equal(expected, actual);
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            var account = ServiceClientFactory.GetStorageAccount();

            var containers = await account.CreateCloudBlobClient().ListContainersAsync(StoragePrefix);
            foreach (var container in containers)
            {
                await container.DeleteAsync();
            }

            var queues = await account.CreateCloudQueueClient().ListQueuesAsync(StoragePrefix);
            foreach (var queue in queues)
            {
                await queue.DeleteAsync();
            }

            var tables = await account.CreateCloudTableClient().ListTablesAsync(StoragePrefix);
            foreach (var table in tables)
            {
                await table.DeleteAsync();
            }
        }
    }
}

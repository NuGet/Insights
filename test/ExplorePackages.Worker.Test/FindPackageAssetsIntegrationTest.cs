using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.Support;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public class FindPackageAssetsIntegrationTest : IClassFixture<DefaultWebApplicationFactory<StaticFilesStartup>>, IAsyncLifetime
    {
        private const string TestData = "TestData";
        private const string FindPackageAssetsDir = "FindPackageAssets";
        private const string FindPackageAssets_WithDeleteDir = "FindPackageAssets_WithDelete";
        private const string Step1 = "Step1";
        private const string Step2 = "Step2";

        public FindPackageAssetsIntegrationTest(
            ITestOutputHelper output,
            DefaultWebApplicationFactory<StaticFilesStartup> factory)
        {
            StoragePrefix = "t" + StorageUtility.GenerateUniqueId();
            HttpMessageHandlerFactory = new TestHttpMessageHandlerFactory();

            var currentDirectory = Directory.GetCurrentDirectory();
            var testWebHostBuilder = factory.WithWebHostBuilder(b => b
                .ConfigureLogging(b => b.SetMinimumLevel(LogLevel.Error))
                .UseContentRoot(currentDirectory)
                .UseWebRoot(currentDirectory));
            TestDataHttpClient = testWebHostBuilder.CreateClient();

            var startup = new Startup();
            Host = new HostBuilder()
                .ConfigureWebJobs(startup.Configure)
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddSingleton<IExplorePackagesHttpMessageHandlerFactory>(HttpMessageHandlerFactory);

                    serviceCollection.AddTransient<WorkerQueueFunction>();

                    serviceCollection.AddSingleton(new TelemetryClient(TelemetryConfiguration.CreateDefault()));

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
        private TestHttpMessageHandlerFactory HttpMessageHandlerFactory { get; }
        public HttpClient TestDataHttpClient { get; }
        public IHost Host { get; }
        public IOptions<ExplorePackagesWorkerSettings> Options { get; }
        public ServiceClientFactory ServiceClientFactory { get; }
        public IWorkerQueueFactory WorkerQueueFactory { get; }
        public CursorStorageService CursorStorageService { get; }
        public CatalogScanStorageService CatalogScanStorageService { get; }
        public CatalogScanService CatalogScanService { get; }
        public ILogger<FindPackageAssetsIntegrationTest> Logger { get; }
        public WorkerQueueFunction Target { get; }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task FindPackageAssets(bool usingBatching)
        {
            Options.Value.MessageBatchSizes[nameof(CatalogLeafScanMessage)] = usingBatching ? 2 : 1;

            Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z");
            var cursorName = $"CatalogScan-{CatalogScanType.FindPackageAssets}";

            await CatalogScanService.InitializeAsync();

            var cursor = await CursorStorageService.GetOrCreateAsync(cursorName);
            cursor.Value = min0;
            await CursorStorageService.UpdateAsync(cursor);

            // Act
            await UpdateFindPackageAssetsAsync(max1);

            // Assert
            await AssertFindPackageAssetsOutputAsync(FindPackageAssetsDir, Step1, 0);
            await AssertFindPackageAssetsOutputAsync(FindPackageAssetsDir, Step1, 1);
            await AssertFindPackageAssetsOutputAsync(FindPackageAssetsDir, Step1, 2);

            // Act
            await UpdateFindPackageAssetsAsync(max2);

            // Assert
            await AssertFindPackageAssetsOutputAsync(FindPackageAssetsDir, Step2, 0);
            await AssertFindPackageAssetsOutputAsync(FindPackageAssetsDir, Step1, 1); // This file is unchanged.
            await AssertFindPackageAssetsOutputAsync(FindPackageAssetsDir, Step2, 2);

            await VerifyExpectedContainers();
        }

        [Fact]
        public async Task FindPackageAssets_WithDelete()
        {
            Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

            // Arrange
            HttpMessageHandlerFactory.OnSendAsync = async req =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/behaviorsample.1.0.0.nupkg"))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestData}/{FindPackageAssets_WithDeleteDir}/behaviorsample.1.0.0.nupkg");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z");
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z");
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z");
            var cursorName = $"CatalogScan-{CatalogScanType.FindPackageAssets}";

            await CatalogScanService.InitializeAsync();

            var cursor = await CursorStorageService.GetOrCreateAsync(cursorName);
            cursor.Value = min0;
            await CursorStorageService.UpdateAsync(cursor);

            // Act
            await UpdateFindPackageAssetsAsync(max1);

            // Assert
            await AssertFindPackageAssetsOutputAsync(FindPackageAssets_WithDeleteDir, Step1, 0);
            await AssertFindPackageAssetsOutputAsync(FindPackageAssets_WithDeleteDir, Step1, 1);
            await AssertFindPackageAssetsOutputAsync(FindPackageAssets_WithDeleteDir, Step1, 2);

            // Act
            await UpdateFindPackageAssetsAsync(max2);

            // Assert
            await AssertFindPackageAssetsOutputAsync(FindPackageAssets_WithDeleteDir, Step1, 0); // This file is unchanged.
            await AssertFindPackageAssetsOutputAsync(FindPackageAssets_WithDeleteDir, Step1, 1); // This file is unchanged.
            await AssertFindPackageAssetsOutputAsync(FindPackageAssets_WithDeleteDir, Step2, 2);

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
                new[] { Options.Value.WorkerQueueName, Options.Value.WorkerQueueName + "-poison" },
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
                    await Target.ProcessAsync(message.AsString);
                    await queue.DeleteMessageAsync(message);
                }

                indexScan = await CatalogScanStorageService.GetIndexScanAsync(indexScan.CursorName, indexScan.ScanId);

                if (indexScan.ParsedState != CatalogScanState.Complete)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
            while (indexScan.ParsedState != CatalogScanState.Complete);
        }

        private async Task AssertFindPackageAssetsOutputAsync(string testName, string stepName, int bucket)
        {
            var client = ServiceClientFactory.GetStorageAccount().CreateCloudBlobClient();
            var container = client.GetContainerReference(Options.Value.FindPackageAssetsContainerName);
            var blob = container.GetBlockBlobReference($"compact_{bucket}.csv");
            var actual = await blob.DownloadTextAsync();
            var expected = File.ReadAllText(Path.Combine(TestData, testName, stepName, blob.Name));
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

        public static HttpRequestMessage Clone(HttpRequestMessage req)
        {
            var clone = new HttpRequestMessage(req.Method, req.RequestUri);

            clone.Content = req.Content;
            clone.Version = req.Version;

            foreach (KeyValuePair<string, object> prop in req.Properties)
            {
                clone.Properties.Add(prop);
            }

            foreach (KeyValuePair<string, IEnumerable<string>> header in req.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }

        private class TestHttpMessageHandlerFactory : IExplorePackagesHttpMessageHandlerFactory
        {
            public Func<HttpRequestMessage, Task<HttpResponseMessage>> OnSendAsync { get; set; }

            public DelegatingHandler Create()
            {
                return new TestHttpMessageHandler(async req =>
                {
                    if (OnSendAsync != null)
                    {
                        return await OnSendAsync(req);
                    }

                    return null;
                });
            }
        }

        private class TestHttpMessageHandler : DelegatingHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _onSendAsync;

            public TestHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> onSendAsync)
            {
                _onSendAsync = onSendAsync;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = await _onSendAsync(request);
                if (response != null)
                {
                    return response;
                }

                return await base.SendAsync(request, cancellationToken);
            }
        }
    }
}

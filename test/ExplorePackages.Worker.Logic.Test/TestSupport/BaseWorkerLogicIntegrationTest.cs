using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Queue;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public abstract class BaseWorkerLogicIntegrationTest : IClassFixture<DefaultWebApplicationFactory<StaticFilesStartup>>, IAsyncLifetime
    {
        public const string ProgramName = "Knapcode.ExplorePackages.Worker.Logic.Test";
        public const string TestData = "TestData";
        public const string Step1 = "Step1";
        public const string Step2 = "Step2";

        /// <summary>
        /// This should only be on when generating new test data locally. It should never be checked in as true.
        /// </summary>
        protected static readonly bool OverwriteTestData = false;

        private readonly Lazy<IHost> _lazyHost;

        public BaseWorkerLogicIntegrationTest(
            ITestOutputHelper output,
            DefaultWebApplicationFactory<StaticFilesStartup> factory)
        {
            Output = output;
            StoragePrefix = "t" + StorageUtility.GenerateUniqueId();
            HttpMessageHandlerFactory = new TestHttpMessageHandlerFactory();

            var currentDirectory = Directory.GetCurrentDirectory();
            var testWebHostBuilder = factory.WithWebHostBuilder(b => b
                .ConfigureLogging(b => b.SetMinimumLevel(LogLevel.Error))
                .UseContentRoot(currentDirectory)
                .UseWebRoot(currentDirectory));
            TestDataHttpClient = testWebHostBuilder.CreateClient();
            LogLevelToCount = new ConcurrentDictionary<LogLevel, int>();

            _lazyHost = new Lazy<IHost>(() => GetHost(output));
        }

        private IHost GetHost(ITestOutputHelper output)
        {
            var hostBuilder = new HostBuilder();

            hostBuilder
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddExplorePackages(ProgramName);
                    serviceCollection.AddExplorePackagesWorker();

                    serviceCollection.AddSingleton((IExplorePackagesHttpMessageHandlerFactory)HttpMessageHandlerFactory);

                    serviceCollection.AddTransient(s => output.GetTelemetryClient());

                    serviceCollection.AddLogging(o =>
                    {
                        o.SetMinimumLevel(LogLevel.Trace);
                        o.AddProvider(new XunitLoggerProvider(output, LogLevel.Trace, LogLevelToCount));
                    });

                    serviceCollection.Configure((Action<ExplorePackagesSettings>)(ConfigureDefaultsAndSettings));
                    serviceCollection.Configure((Action<ExplorePackagesWorkerSettings>)(ConfigureDefaultsAndSettings));
                });

            ConfigureHostBuilder(hostBuilder);

            return hostBuilder.Build();
        }

        protected virtual void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
        }

        private void ConfigureDefaultsAndSettings(ExplorePackagesSettings x)
        {
            x.StorageContainerName = $"{StoragePrefix}1p1";
            x.LeaseContainerName = $"{StoragePrefix}1l1";

            if (ConfigureSettings != null)
            {
                ConfigureSettings(x);
            }
        }

        private void ConfigureDefaultsAndSettings(ExplorePackagesWorkerSettings x)
        {
            x.AppendResultStorageBucketCount = 3;
            x.AppendResultUniqueIds = false;

            x.WorkerQueueName = $"{StoragePrefix}1wq1";
            x.CursorTableName = $"{StoragePrefix}1c1";
            x.CatalogIndexScanTableName = $"{StoragePrefix}1cis1";
            x.CatalogPageScanTableName = $"{StoragePrefix}1cps1";
            x.CatalogLeafScanTableName = $"{StoragePrefix}1cls1";
            x.TaskStateTableName = $"{StoragePrefix}1ts1";
            x.CsvRecordTableName = $"{StoragePrefix}1cr1";
            x.LatestPackageLeafTableName = $"{StoragePrefix}1lpl1";
            x.PackageAssetContainerName = $"{StoragePrefix}1fpa1";
            x.PackageAssemblyContainerName = $"{StoragePrefix}1fpi1";
            x.RealRestoreContainerName = $"{StoragePrefix}1rrr1";
            x.CatalogLeafItemContainerName = $"{StoragePrefix}1fcli1";
            x.PackageDownloadsContainerName = $"{StoragePrefix}1pd1";

            ConfigureDefaultsAndSettings((ExplorePackagesSettings)x);

            if (ConfigureWorkerSettings != null)
            {
                ConfigureWorkerSettings(x);
            }
        }

        public ITestOutputHelper Output { get; }
        public string StoragePrefix { get; }
        public TestHttpMessageHandlerFactory HttpMessageHandlerFactory { get; }
        public HttpClient TestDataHttpClient { get; }
        public ConcurrentDictionary<LogLevel, int> LogLevelToCount { get; }
        public Action<ExplorePackagesSettings> ConfigureSettings { get; set; }
        public Action<ExplorePackagesWorkerSettings> ConfigureWorkerSettings { get; set; }
        public IHost Host => _lazyHost.Value;
        public IOptions<ExplorePackagesWorkerSettings> Options => Host.Services.GetRequiredService<IOptions<ExplorePackagesWorkerSettings>>();
        public ServiceClientFactory ServiceClientFactory => Host.Services.GetRequiredService<ServiceClientFactory>();
        public CatalogScanService CatalogScanService => Host.Services.GetRequiredService<CatalogScanService>();
        public CursorStorageService CursorStorageService => Host.Services.GetRequiredService<CursorStorageService>();
        public CatalogScanStorageService CatalogScanStorageService => Host.Services.GetRequiredService<CatalogScanStorageService>();
        public TaskStateStorageService TaskStateStorageService => Host.Services.GetRequiredService<TaskStateStorageService>();
        public IMessageEnqueuer MessageEnqueuer => Host.Services.GetRequiredService<IMessageEnqueuer>();
        public IWorkerQueueFactory WorkerQueueFactory => Host.Services.GetRequiredService<IWorkerQueueFactory>();
        public ITelemetryClient TelemetryClient => Host.Services.GetRequiredService<ITelemetryClient>();
        public ILogger Logger => Host.Services.GetRequiredService<ILogger<BaseWorkerLogicIntegrationTest>>();

        protected async Task SetCursorAsync(CatalogScanDriverType driverType, DateTimeOffset min)
        {
            var cursor = await CatalogScanService.GetCursorAsync(driverType);
            cursor.Value = min;
            await CursorStorageService.UpdateAsync(cursor);
        }

        public ConcurrentBag<CatalogIndexScan> ExpectedCatalogIndexScans { get; } = new ConcurrentBag<CatalogIndexScan>();

        protected async Task<CatalogIndexScan> UpdateAsync(CatalogScanDriverType driverType, bool? onlyLatestLeaves, DateTimeOffset max)
        {
            var indexScan = await CatalogScanService.UpdateAsync(driverType, max, onlyLatestLeaves);
            return await UpdateAsync(indexScan);
        }

        protected async Task<CatalogIndexScan> UpdateAsync(CatalogIndexScan indexScan)
        {
            await ProcessQueueAsync(() => { }, async () =>
            {
                indexScan = await CatalogScanStorageService.GetIndexScanAsync(indexScan.CursorName, indexScan.ScanId);

                if (indexScan.ParsedState != CatalogScanState.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    return false;
                }

                return true;
            });

            ExpectedCatalogIndexScans.Add(indexScan);

            return indexScan;
        }

        protected async Task UpdateAsync(TaskStateKey taskStateKey)
        {
            await ProcessQueueAsync(() => { }, async () =>
            {
                var countLowerBound = await TaskStateStorageService.GetCountLowerBoundAsync(taskStateKey.StorageSuffix, taskStateKey.PartitionKey);
                if (countLowerBound > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    return false;
                }

                return true;
            });
        }

        protected async Task ProcessQueueAsync(Action foundMessage, Func<Task<bool>> isCompleteAsync)
        {
            var queue = WorkerQueueFactory.GetQueue();
            bool isComplete;
            do
            {
                CloudQueueMessage message;
                while ((message = await queue.GetMessageAsync()) != null)
                {
                    foundMessage();
                    using (var scope = Host.Services.CreateScope())
                    {
                        await ProcessMessageAsync(scope.ServiceProvider, message);
                    }

                    await queue.DeleteMessageAsync(message);
                }

                isComplete = await isCompleteAsync();
            }
            while (!isComplete);
        }

        protected virtual async Task ProcessMessageAsync(IServiceProvider serviceProvider, CloudQueueMessage message)
        {
            var leaseScope = serviceProvider.GetRequiredService<TempStreamLeaseScope>();
            await using var scopeOwnership = leaseScope.TakeOwnership();
            var messageProcessor = serviceProvider.GetRequiredService<IGenericMessageProcessor>();
            await messageProcessor.ProcessSingleAsync(message.AsString, message.DequeueCount);
        }

        protected async Task AssertCompactAsync(string containerName, string testName, string stepName, int bucket)
        {
            await AssertBlobAsync(containerName, testName, stepName, $"compact_{bucket}.csv");
        }

        protected void AssertOnlyInfoLogsOrLess()
        {
            var warningOrGreater = LogLevelToCount
                .Where(x => x.Key >= LogLevel.Warning)
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Key)
                .ToList();
            foreach ((var logLevel, var count) in warningOrGreater)
            {
                Logger.LogInformation("There were {Count} {LogLevel} log messages.", count, logLevel);
            }
            Assert.Empty(warningOrGreater);
        }

        protected async Task AssertBlobAsync(string containerName, string testName, string stepName, string blobName, bool gzip = false)
        {
            var client = ServiceClientFactory.GetStorageAccount().CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);
            var blob = container.GetBlockBlobReference(blobName);

            string actual;
            var fileName = blobName;
            if (gzip)
            {
                using var destStream = new MemoryStream();
                await blob.DownloadToStreamAsync(destStream);
                destStream.Position = 0;
                using var gzipStream = new GZipStream(destStream, CompressionMode.Decompress);
                using var reader = new StreamReader(gzipStream);
                actual = await reader.ReadToEndAsync();

                if (blobName.EndsWith(".gz"))
                {
                    fileName = blobName.Substring(0, blobName.Length - ".gz".Length);
                }
            }
            else
            {
                actual = await blob.DownloadTextAsync();
            }

            if (OverwriteTestData)
            {
                Directory.CreateDirectory(Path.Combine(TestData, testName, stepName));
                File.WriteAllText(Path.Combine(TestData, testName, stepName, fileName), actual);
            }
            var expected = File.ReadAllText(Path.Combine(TestData, testName, stepName, fileName));
            Assert.Equal(expected, actual);
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

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
            var clone = new HttpRequestMessage(req.Method, req.RequestUri)
            {
                Content = req.Content,
                Version = req.Version
            };

            foreach (var prop in req.Properties)
            {
                clone.Properties.Add(prop);
            }

            foreach (var header in req.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }

        public class TestHttpMessageHandlerFactory : IExplorePackagesHttpMessageHandlerFactory
        {
            public Func<HttpRequestMessage, Task<HttpResponseMessage>> OnSendAsync { get; set; }

            public ConcurrentQueue<HttpRequestMessage> Requests { get; } = new ConcurrentQueue<HttpRequestMessage>();

            public DelegatingHandler Create()
            {
                return new TestHttpMessageHandler(async req =>
                {
                    Requests.Enqueue(req);

                    if (OnSendAsync != null)
                    {
                        return await OnSendAsync(req);
                    }

                    return null;
                });
            }
        }

        public class TestHttpMessageHandler : DelegatingHandler
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

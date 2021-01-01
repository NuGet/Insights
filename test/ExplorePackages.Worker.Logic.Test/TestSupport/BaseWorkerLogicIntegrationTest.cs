using System;
using System.Collections.Generic;
using System.IO;
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
        public const string TestData = "TestData";
        public const string Step1 = "Step1";
        public const string Step2 = "Step2";

        private readonly Lazy<IHost> _lazyHost;

        public BaseWorkerLogicIntegrationTest(
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

            _lazyHost = new Lazy<IHost>(() => GetHost(output));
        }

        private IHost GetHost(ITestOutputHelper output)
        {
            var hostBuilder = new HostBuilder();

            ConfigureHostBuilder(hostBuilder);

            return hostBuilder
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddExplorePackages("Knapcode.ExplorePackages.Worker.Logic.Test");
                    serviceCollection.AddExplorePackagesWorker();

                    serviceCollection.AddSingleton((IExplorePackagesHttpMessageHandlerFactory)HttpMessageHandlerFactory);

                    serviceCollection.AddTransient(s => output.GetTelemetryClient());

                    serviceCollection.AddLogging(o =>
                    {
                        o.SetMinimumLevel(LogLevel.Trace);
                        o.AddProvider(new XunitLoggerProvider(output));
                    });

                    serviceCollection.Configure((Action<ExplorePackagesSettings>)(ConfigureDefaultsAndSettings));
                    serviceCollection.Configure((Action<ExplorePackagesWorkerSettings>)(ConfigureDefaultsAndSettings));
                })
                .Build();
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
            x.CsvRecordsTableName = $"{StoragePrefix}1cr1";
            x.LatestLeavesTableName = $"{StoragePrefix}1ll1";
            x.FindPackageAssetsContainerName = $"{StoragePrefix}1fpa1";
            x.FindPackageAssembliesContainerName = $"{StoragePrefix}1fpi1";
            x.RunRealRestoreContainerName = $"{StoragePrefix}1rrr1";
            x.FindCatalogLeafItemsContainerName = $"{StoragePrefix}1fcli1";

            ConfigureDefaultsAndSettings((ExplorePackagesSettings)x);

            if (ConfigureWorkerSettings != null)
            {
                ConfigureWorkerSettings(x);
            }
        }

        public string StoragePrefix { get; }
        public TestHttpMessageHandlerFactory HttpMessageHandlerFactory { get; }
        public HttpClient TestDataHttpClient { get; }

        public Action<ExplorePackagesSettings> ConfigureSettings { get; set; }
        public Action<ExplorePackagesWorkerSettings> ConfigureWorkerSettings { get; set; }
        public IHost Host => _lazyHost.Value;
        public IOptions<ExplorePackagesWorkerSettings> Options => Host.Services.GetRequiredService<IOptions<ExplorePackagesWorkerSettings>>();
        public ServiceClientFactory ServiceClientFactory => Host.Services.GetRequiredService<ServiceClientFactory>();
        public CatalogScanService CatalogScanService => Host.Services.GetRequiredService<CatalogScanService>();
        public CursorStorageService CursorStorageService => Host.Services.GetRequiredService<CursorStorageService>();
        public CatalogScanStorageService CatalogScanStorageService => Host.Services.GetRequiredService<CatalogScanStorageService>();
        public TaskStateStorageService TaskStateStorageService => Host.Services.GetRequiredService<TaskStateStorageService>();
        public IWorkerQueueFactory WorkerQueueFactory => Host.Services.GetRequiredService<IWorkerQueueFactory>();
        public ITelemetryClient TelemetryClient => Host.Services.GetRequiredService<ITelemetryClient>();
        public ILogger Logger => Host.Services.GetRequiredService<ILogger<BaseWorkerLogicIntegrationTest>>();

        protected async Task SetCursorAsync(CatalogScanDriverType driverType, DateTimeOffset min)
        {
            var cursor = await CatalogScanService.GetCursorAsync(driverType);
            cursor.Value = min;
            await CursorStorageService.UpdateAsync(cursor);
        }

        protected async Task<CatalogIndexScan> UpdateAsync(CatalogScanDriverType driverType, DateTimeOffset max)
        {
            var indexScan = await CatalogScanService.UpdateAsync(driverType, max);

            await ProcessQueueAsync(() => { }, async () =>
            {
                indexScan = await CatalogScanStorageService.GetIndexScanAsync(indexScan.CursorName, indexScan.ScanId);

                if (indexScan.ParsedState != CatalogScanState.Complete)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    return false;
                }

                return true;
            });

            return indexScan;
        }

        protected async Task ProcessQueueAsync()
        {
            var completeCount = 0;
            await ProcessQueueAsync(() => completeCount = 0, async () =>
            {
                if (completeCount >= 5)
                {
                    return true;
                }

                completeCount++;
                await Task.Delay(100);
                return false;
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
            var messageProcessor = serviceProvider.GetRequiredService<GenericMessageProcessor>();
            await messageProcessor.ProcessAsync(message.AsString, message.DequeueCount);
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

        public class TestHttpMessageHandlerFactory : IExplorePackagesHttpMessageHandlerFactory
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

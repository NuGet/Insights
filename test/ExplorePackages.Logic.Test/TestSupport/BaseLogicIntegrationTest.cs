using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages
{
    public abstract class BaseLogicIntegrationTest : IClassFixture<DefaultWebApplicationFactory<StaticFilesStartup>>, IAsyncLifetime
    {
        static BaseLogicIntegrationTest()
        {
            var oldTemp = Environment.GetEnvironmentVariable("TEMP");
            var newTemp = Path.GetFullPath(Path.Join(oldTemp, "Knapcode.ExplorePackages.Temp"));
            Directory.CreateDirectory(newTemp);
            Environment.SetEnvironmentVariable("TEMP", newTemp);
            Environment.SetEnvironmentVariable("TMP", newTemp);
        }

        public const string ProgramName = "Knapcode.ExplorePackages.Logic.Test";
        public const string TestData = "TestData";
        public const string Step1 = "Step1";
        public const string Step2 = "Step2";
        public static readonly Regex StoragePrefixPattern = new Regex(@"t(?<Date>\d{6})[a-z234567]{10}");

        /// <summary>
        /// This should only be on when generating new test data locally. It should never be checked in as true.
        /// </summary>
        protected static readonly bool OverwriteTestData = false;

        private readonly Lazy<IHost> _lazyHost;

        public BaseLogicIntegrationTest(
            ITestOutputHelper output,
            DefaultWebApplicationFactory<StaticFilesStartup> factory)
        {
            Output = output;

            var randomBytes = new byte[6];
            ThreadLocalRandom.NextBytes(randomBytes);
            StoragePrefix = "t" + DateTimeOffset.UtcNow.ToString("yyMMdd") + randomBytes.ToTrimmedBase32();
            Assert.Matches(StoragePrefixPattern, StoragePrefix);

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

                    serviceCollection.AddSingleton((IExplorePackagesHttpMessageHandlerFactory)HttpMessageHandlerFactory);

                    serviceCollection.AddTransient(s => output.GetTelemetryClient());

                    serviceCollection.AddLogging(o =>
                    {
                        o.SetMinimumLevel(LogLevel.Trace);
                        o.AddProvider(new XunitLoggerProvider(output, LogLevel.Trace, LogLevelToCount, FailFastLogLevel));
                    });

                    serviceCollection.Configure((Action<ExplorePackagesSettings>)ConfigureDefaultsAndSettings);
                });

            ConfigureHostBuilder(hostBuilder);

            return hostBuilder.Build();
        }

        protected LogLevel FailFastLogLevel { get; set; } = LogLevel.Error;
        protected LogLevel AssertLogLevel { get; set; } = LogLevel.Warning;

        protected virtual void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
        }

        protected void ConfigureDefaultsAndSettings(ExplorePackagesSettings x)
        {
            x.StorageConnectionString = TestSettings.StorageConnectionString;

            x.StorageContainerName = $"{StoragePrefix}1p1";
            x.LeaseContainerName = $"{StoragePrefix}1l1";
            x.PackageArchiveTableName = $"{StoragePrefix}1pa1";
            x.PackageManifestTableName = $"{StoragePrefix}1pm1";
            x.TimerTableName = $"{StoragePrefix}1t1";

            if (ConfigureSettings != null)
            {
                ConfigureSettings(x);
            }

            AssertStoragePrefix(x);
        }

        protected void AssertStoragePrefix(object x)
        {
            // Verify all container names are prefixed, so that parallel tests and cleanup work properly.
            var storageNameProperties = x
                .GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(x => x.Name.EndsWith("QueueName") || x.Name.EndsWith("TableName") || x.Name.EndsWith("ContainerName"));
            var storageNames = new HashSet<string>();
            foreach (var property in storageNameProperties)
            {
                var value = (string)property.GetMethod.Invoke(x, null);
                Assert.StartsWith(StoragePrefix, value);
                Assert.DoesNotContain(value, storageNames); // Make sure there are no duplicates
                storageNames.Add(value);
            }
        }

        public ITestOutputHelper Output { get; }
        public string StoragePrefix { get; }
        public TestHttpMessageHandlerFactory HttpMessageHandlerFactory { get; }
        public HttpClient TestDataHttpClient { get; }
        public ConcurrentDictionary<LogLevel, int> LogLevelToCount { get; }
        public Action<ExplorePackagesSettings> ConfigureSettings { get; set; }
        public IHost Host => _lazyHost.Value;
        public ServiceClientFactory ServiceClientFactory => Host.Services.GetRequiredService<ServiceClientFactory>();
        public ITelemetryClient TelemetryClient => Host.Services.GetRequiredService<ITelemetryClient>();
        public ILogger Logger => Host.Services.GetRequiredService<ILogger<BaseLogicIntegrationTest>>();

        protected async Task AssertBlobCountAsync(string containerName, int expected)
        {
            var client = ServiceClientFactory.GetStorageAccount().CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);
            var blobs = await container.ListBlobsAsync(TelemetryClient.StartQueryLoopMetrics());
            Assert.Equal(expected, blobs.Count);
        }

        protected async Task AssertCsvBlobAsync<T>(string containerName, string testName, string stepName, string blobName) where T : ICsvRecord<T>, new()
        {
            Assert.EndsWith(".csv.gz", blobName);
            var actual = await AssertBlobAsync(containerName, testName, stepName, blobName, gzip: true);
            var headerFactory = new T();
            var stringWriter = new StringWriter();
            headerFactory.WriteHeader(stringWriter);
            Assert.StartsWith(stringWriter.ToString(), actual);
        }

        protected async Task<string> AssertBlobAsync(string containerName, string testName, string stepName, string blobName, bool gzip = false)
        {
            var client = ServiceClientFactory.GetStorageAccount().CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);
            var blob = container.GetBlockBlobReference(blobName);

            string actual;
            var fileName = blobName;
            if (gzip)
            {
                Assert.EndsWith(".gz", blobName);
                fileName = blobName.Substring(0, blobName.Length - ".gz".Length);

                using var destStream = new MemoryStream();
                await blob.DownloadToStreamAsync(destStream);
                destStream.Position = 0;

                Assert.Contains("rawSizeBytes", blob.Metadata);
                var uncompressedLength = long.Parse(blob.Metadata["rawSizeBytes"]);

                using var gzipStream = new GZipStream(destStream, CompressionMode.Decompress);
                using var decompressedStream = new MemoryStream();
                await gzipStream.CopyToAsync(decompressedStream);
                decompressedStream.Position = 0;

                Assert.Equal(uncompressedLength, decompressedStream.Length);

                using var reader = new StreamReader(decompressedStream);
                actual = await reader.ReadToEndAsync();
            }
            else
            {
                actual = await blob.DownloadTextAsync();
            }

            // Normalize line ending, since there are all kinds of nasty mixtures between Environment.NewLine and Git
            // settings.
            actual = Regex.Replace(actual, @"\r\n|\n", Environment.NewLine);

            if (OverwriteTestData)
            {
                Directory.CreateDirectory(Path.Combine(TestData, testName, stepName));
                File.WriteAllText(Path.Combine(TestData, testName, stepName, fileName), actual);
            }
            var expected = File.ReadAllText(Path.Combine(TestData, testName, stepName, fileName));
            Assert.Equal(expected, actual);

            return actual;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public virtual async Task DisposeAsync()
        {
            try
            {
                // Global assertions
                AssertOnlyInfoLogsOrLess();
            }
            finally
            {
                // Clean up
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

        private void AssertOnlyInfoLogsOrLess()
        {
            var warningOrGreater = LogLevelToCount
                .Where(x => x.Key >= AssertLogLevel)
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Key)
                .ToList();
            foreach ((var logLevel, var count) in warningOrGreater)
            {
                Logger.LogInformation("There were {Count} {LogLevel} log messages.", count, logLevel);
            }
            Assert.Empty(warningOrGreater);
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

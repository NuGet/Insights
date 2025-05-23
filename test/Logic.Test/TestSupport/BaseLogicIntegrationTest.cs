// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using MessagePack;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using NuGet.Insights.MemoryStorage;
using NuGet.Insights.WideEntities;

namespace NuGet.Insights
{
    public abstract class BaseLogicIntegrationTest : IClassFixture<DefaultWebApplicationFactory<StaticFilesStartup>>, IAsyncLifetime
    {
        static BaseLogicIntegrationTest()
        {
            // move temp directory to isolate test file leaks
            var oldTemp = Environment.GetEnvironmentVariable("TEMP");
            var newTemp = Path.GetFullPath(Path.Join(oldTemp, "NuGet.Insights.Temp"));
            Directory.CreateDirectory(newTemp);
            Environment.SetEnvironmentVariable("TEMP", newTemp);
            Environment.SetEnvironmentVariable("TMP", newTemp);

            // update the last modified for test input
            SetLastModified("DownloadsToCsv", Step1, "downloads.v1.json", "2021-01-14T18:00:00Z");
            SetLastModified("DownloadsToCsv", Step2, "downloads.v1.json", "2021-01-15T19:00:00Z");
            SetLastModified("DownloadsToCsv", Step1, "downloads.v2.json", "2021-01-14T18:30:00Z");
            SetLastModified("DownloadsToCsv", Step2, "downloads.v2.json", "2021-01-15T19:30:00Z");

            SetLastModified("DownloadsToCsv_Duplicates", Step1, "downloads.v1.json", "2021-01-14T18:00:00Z");

            SetLastModified("DownloadsToCsv_UnicodeDuplicates", Step1, "downloads.v1.json", "2021-01-14T18:00:00Z");

            SetLastModified("OwnersToCsv", Step1, "owners.v2.json", "2021-01-14T18:00:00Z");
            SetLastModified("OwnersToCsv", Step2, "owners.v2.json", "2021-01-15T19:00:00Z");

            SetLastModified("VerifiedPackagesToCsv", Step1, "verifiedPackages.json", "2021-01-14T18:00:00Z");
            SetLastModified("VerifiedPackagesToCsv", Step2, "verifiedPackages.json", "2021-01-15T19:00:00Z");

            SetLastModified("ExcludedPackagesToCsv", Step1, "excludedPackages.json", "2021-01-14T18:00:00Z");
            SetLastModified("ExcludedPackagesToCsv", Step2, "excludedPackages.json", "2021-01-15T19:00:00Z");

            SetLastModified("PopularityTransfersToCsv", Step1, "popularity-transfers.v1.json", "2021-01-16T18:00:00Z");
            SetLastModified("PopularityTransfersToCsv", Step2, "popularity-transfers.v1.json", "2021-01-17T19:00:00Z");

            SetLastModified("GitHubUsageToCsv", Step1, "GitHubUsage.v1.json", "2021-01-16T20:00:00Z");
            SetLastModified("GitHubUsageToCsv", Step2, "GitHubUsage.v1.json", "2021-01-17T21:00:00Z");

            SetLastModified("behaviorsample.1.0.0.nupkg.testdata", "2021-01-14T18:00:00Z");
            SetLastModified("behaviorsample.1.0.0.nuspec", "2021-01-14T19:00:00Z");
            SetLastModified("behaviorsample.1.0.0.md", "2021-01-14T20:00:00Z");
            SetLastModified("behaviorsample.1.0.0.snupkg.testdata", "2021-01-14T21:00:00Z");

            SetLastModified("deltax.1.0.0.nupkg.testdata", "2021-01-14T18:00:00Z");
        }

        private static void SetLastModified(string testName, string stepName, string fileName, string lastModified)
        {
            SetLastModified(Path.Combine(testName, stepName, fileName), lastModified);
        }

        protected static void SetLastModified(string path, string lastModified)
        {
            var fileInfo = new FileInfo(Path.Combine(TestInput, path));
            var parsedLastModified = DateTimeOffset.Parse(lastModified, CultureInfo.InvariantCulture);
            fileInfo.LastWriteTimeUtc = parsedLastModified.UtcDateTime;
        }

        public const string UserAgentAppName = "NuGet.Insights.Logic.Test";
        public const string TestInput = "TestInput";
        public const string TestData = "TestData";
        public const string Step1 = "Step1";
        public const string Step2 = "Step2";
        public const string Step3 = "Step3";

        private readonly Lazy<IHost> _lazyHost;

        public BaseLogicIntegrationTest(
            ITestOutputHelper output,
            DefaultWebApplicationFactory<StaticFilesStartup> factory)
        {
            // Remove xUnit synchronization context to avoid deadlocks in async tests.
            // This is related to the usage of FakeTimeProvider.
            // https://github.com/dotnet/extensions/tree/main/src/Libraries/Microsoft.Extensions.TimeProvider.Testing#synchronizationcontext-in-xunit-tests
            SynchronizationContext.SetSynchronizationContext(null);

            Output = output;
            WebApplicationFactory = factory;
            StoragePrefix = LogicTestSettings.NewStoragePrefix();
            HttpMessageHandlerFactory = new TestHttpMessageHandlerFactory(output.GetLoggerFactory());

            TimeProvider = new Mock<TimeProvider>() { CallBase = true };
            TimeProvider.Setup(x => x.GetUtcNow()).Returns(() => UtcNow ?? DateTimeOffset.UtcNow);

            MemoryBlobServiceStore = new MemoryBlobServiceStore(TimeProvider.Object);
            MemoryQueueServiceStore = new MemoryQueueServiceStore(TimeProvider.Object);
            MemoryTableServiceStore = new MemoryTableServiceStore(TimeProvider.Object);

            var currentDirectory = Directory.GetCurrentDirectory();
            var testWebHostBuilder = factory.WithWebHostBuilder(b => b
                .ConfigureLogging(b => b.SetMinimumLevel(LogLevel.Error))
                .UseContentRoot(currentDirectory)
                .UseWebRoot(currentDirectory));
            TestDataHttpClient = testWebHostBuilder.CreateClient();
            LogLevelToCount = new ConcurrentDictionary<LogLevel, int>();

            _lazyHost = new Lazy<IHost>(() => GetHost(output));
        }

        protected IHost GetHost(ITestOutputHelper output)
        {
            var hostBuilder = new HostBuilder();

            hostBuilder
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddNuGetInsights(userAgentAppName: UserAgentAppName);

                    serviceCollection.AddSingleton((INuGetInsightsHttpMessageHandlerFactory)HttpMessageHandlerFactory);

                    serviceCollection.AddSingleton(s => new LoggerTelemetryClient(
                        TestOutputHelperExtensions.ShouldIgnoreMetricLog,
                        s.GetRequiredService<ILogger<LoggerTelemetryClient>>()));
                    serviceCollection.AddSingleton<ITelemetryClient>(s => s.GetRequiredService<LoggerTelemetryClient>());

                    serviceCollection.AddSingleton(TimeProvider.Object);

                    serviceCollection.AddSingleton(MemoryBlobServiceStore);
                    serviceCollection.AddSingleton(MemoryQueueServiceStore);
                    serviceCollection.AddSingleton(MemoryTableServiceStore);

                    serviceCollection.AddLogging(o =>
                    {
                        o.SetMinimumLevel(LogLevel.Trace);
                        o.AddProvider(new XunitLoggerProvider(
                            output,
                            LogLevel.Trace,
                            LogLevelToCount,
                            TransformLogLevel,
                            FailFastLogLevel,
                            LogMessages));
                    });

                    serviceCollection.Configure((Action<NuGetInsightsSettings>)AssertDefaultsAndSettings);
                });

            ConfigureHostBuilder(hostBuilder);

            return hostBuilder.Build();
        }

        protected void AssertStoragePrefix<T>(T settings) where T : NuGetInsightsSettings
        {
            // Verify all container names are prefixed, so that parallel tests and cleanup work properly.
            var storageNames = new HashSet<string>();
            foreach (var property in GetStorageNameProperties(settings))
            {
                Assert.StartsWith(StoragePrefix, property.Value, StringComparison.Ordinal);
                Assert.DoesNotContain(property.Value, storageNames); // Make sure there are no duplicates
                storageNames.Add(property.Value);
            }
        }

        protected void InitializeStoragePrefix<T>(T settings) where T : NuGetInsightsSettings
        {
            string ApplyStoragePrefix(string name)
            {
                const int boundary = 1;
                return $"{StoragePrefix}{boundary}{name}{boundary}";
            }

            var properties = GetStorageNameProperties(settings);
            var usedNames = new HashSet<string>(properties.Where(x => !x.DirectProperty).Select(x => x.Value));
            foreach (var property in properties.Where(x => x.DirectProperty))
            {
                var words = Regex
                    .Matches(property.Name, "([A-Z][^A-Z]*)")
                    .Select(m => m.Value)
                    .SkipLast(2) // skip "QueueName", "TableName", "ContainerName"
                    .ToList();

                // try the acronym first
                var name = string.Join(string.Empty, words.Select(x => x[0])).ToLowerInvariant();
                if (!usedNames.Add(ApplyStoragePrefix(name)))
                {
                    var maxWordLength = words.Max(x => x.Length);
                    var wordIndex = 0;
                    var wordLength = 2;
                    var pieces = words.Select(x => x[0].ToString()).ToList();
                    name = string.Empty;
                    do
                    {
                        var word = words[wordIndex];
                        if (wordLength <= word.Length)
                        {
                            pieces[wordIndex] = word.Substring(0, wordLength);
                        }

                        wordIndex++;

                        if (wordIndex >= words.Count)
                        {
                            wordIndex = 0;
                            wordLength++;
                        }

                        if (wordLength > maxWordLength)
                        {
                            throw new InvalidOperationException($"Could not generate a name for storage property {property.Name}.");
                        }

                        name = string.Join(string.Empty, pieces).ToLowerInvariant();
                    }
                    while (!usedNames.Add(ApplyStoragePrefix(name)));
                }

                property.SetValue(ApplyStoragePrefix(name));
            }
        }

        protected enum StorageType
        {
            Blob,
            Queue,
            Table,
            TablePrefix,
        }

        protected record StorageProperty(
            string Name,
            string Value,
            Action<string> SetValue,
            StorageType StorageType,
            bool DirectProperty);

        /// <summary>
        /// These are property names on <see cref="NuGetInsightsSettings"/> (or child classes) that look like
        /// name or name patterns for Azure Storage entities, but are not.
        /// </summary>
        private static readonly FrozenSet<string> NonStoragePropertyNames = new string[]
        {
            "FlatContainerBaseUrlOverride",
            "KustoOldTableNameFormat",
            "KustoTableDocstringFormat",
            "KustoTableFolder",
            "KustoTableNameFormat",
            "KustoTempTableNameFormat",
            "PackagesContainerBaseUrl",
            "SymbolPackagesContainerBaseUrl",
        }.ToFrozenSet();

        protected static List<StorageProperty> GetStorageNameProperties<T>(T settings) where T : NuGetInsightsSettings
        {
            int CountBaseTypes(Type type)
            {
                var count = 0;
                while (type != null)
                {
                    count++;
                    type = type.BaseType;
                }

                return count;
            }

            StorageType? GetStorageType(string propertyName)
            {
                if (propertyName.EndsWith("ContainerName", StringComparison.Ordinal))
                {
                    return StorageType.Blob;
                }

                if (propertyName.EndsWith("QueueName", StringComparison.Ordinal))
                {
                    return StorageType.Queue;
                }

                if (propertyName.EndsWith("TableName", StringComparison.Ordinal))
                {
                    return StorageType.Table;
                }

                if (propertyName.EndsWith("TableNamePrefix", StringComparison.Ordinal))
                {
                    return StorageType.TablePrefix;
                }

                if (!NonStoragePropertyNames.Contains(propertyName)
                    && (propertyName.Contains("Container", StringComparison.OrdinalIgnoreCase)
                        || propertyName.Contains("Queue", StringComparison.OrdinalIgnoreCase)
                        || propertyName.Contains("Table", StringComparison.OrdinalIgnoreCase)
                        || propertyName.Contains("Blob", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new NotSupportedException($"Property name {propertyName} looks like an Azure storage entity name, but cannot be categorized.");
                }

                return null;
            }

            return typeof(T)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(x => x.PropertyType == typeof(string))
                .Select(x => (PropertyInfo: x, StorageType: GetStorageType(x.Name)!))
                .Where(x => x.StorageType.HasValue)
                .OrderBy(x => CountBaseTypes(x.PropertyInfo.DeclaringType))
                .ThenBy(x => x.PropertyInfo.Name)
                .Select(x => new StorageProperty(
                    x.PropertyInfo.Name,
                    Value: (string)x.PropertyInfo.GetMethod.Invoke(settings, null),
                    SetValue: v => x.PropertyInfo.SetMethod.Invoke(settings, [v]),
                    StorageType: x.StorageType.Value,
                    DirectProperty: x.PropertyInfo.DeclaringType == typeof(T)))
                .ToList();
        }

        protected LogLevel FailFastLogLevel { get; set; } = LogLevel.Error;
        protected LogLevel AssertLogLevel { get; set; } = LogLevel.Warning;
        public Func<LogLevel, string, LogLevel> TransformLogLevel { get; set; } = (LogLevel logLevel, string message) =>
        {
            if (message.StartsWith(LoggerExtensions.TransientPrefix, StringComparison.Ordinal) && logLevel == LogLevel.Warning)
            {
                return LogLevel.Information;
            }

            return logLevel;
        };

        protected virtual void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
        }

        protected void AssertDefaultsAndSettings(NuGetInsightsSettings x)
        {
            ConfigureDefaultsAndSettings(x);
            AssertStoragePrefix(x);
        }

        protected void ConfigureAllAuxiliaryFiles(NuGetInsightsSettings settings)
        {
            settings.DownloadsV1Urls = new List<string> { $"http://localhost/{TestInput}/DownloadsToCsv/{Step1}/downloads.v1.json" };
            settings.OwnersV2Urls = new List<string> { $"http://localhost/{TestInput}/OwnersToCsv/{Step1}/owners.v2.json" };
            settings.VerifiedPackagesV1Urls = new List<string> { $"http://localhost/{TestInput}/VerifiedPackagesToCsv/{Step1}/verifiedPackages.json" };
            settings.ExcludedPackagesV1Urls = new List<string> { $"http://localhost/{TestInput}/ExcludedPackagesToCsv/{Step1}/excludedPackages.json" };
            settings.PopularityTransfersV1Urls = new List<string> { $"http://localhost/{TestInput}/PopularityTransfersToCsv/{Step1}/popularity-transfers.v1.json" };
            settings.GitHubUsageV1Urls = new List<string> { $"http://localhost/{TestInput}/GitHubUsageToCsv/{Step1}/GitHubUsage.v1.json" };
        }

        protected void ConfigureDefaultsAndSettings(NuGetInsightsSettings x)
        {
            x.WithTestStorageSettings();

            x.HttpClientAddRetryJitter = false;
            x.HttpClientMaxRetries = 2;
            x.HttpClientMaxRetryDelay = TimeSpan.FromMilliseconds(500);

            x.DownloadsV1AgeLimit = TimeSpan.MaxValue;
            x.DownloadsV2AgeLimit = TimeSpan.MaxValue;
            x.OwnersV2AgeLimit = TimeSpan.MaxValue;
            x.VerifiedPackagesV1AgeLimit = TimeSpan.MaxValue;
            x.ExcludedPackagesV1AgeLimit = TimeSpan.MaxValue;
            x.PopularityTransfersV1AgeLimit = TimeSpan.MaxValue;
            x.GitHubUsageV1AgeLimit = TimeSpan.MaxValue;

            InitializeStoragePrefix(x);

            if (ConfigureSettings != null)
            {
                ConfigureSettings(x);
            }
        }

        public ITestOutputHelper Output { get; }
        public DefaultWebApplicationFactory<StaticFilesStartup> WebApplicationFactory { get; }
        public string StoragePrefix { get; }
        public TestHttpMessageHandlerFactory HttpMessageHandlerFactory { get; }
        public HttpClient TestDataHttpClient { get; }
        public ConcurrentDictionary<LogLevel, int> LogLevelToCount { get; }
        public Action<NuGetInsightsSettings> ConfigureSettings { get; set; }
        public Mock<TimeProvider> TimeProvider { get; }
        public DateTimeOffset? UtcNow { get; set; }
        public MemoryBlobServiceStore MemoryBlobServiceStore { get; }
        public MemoryQueueServiceStore MemoryQueueServiceStore { get; }
        public MemoryTableServiceStore MemoryTableServiceStore { get; }
        public IHost Host => _lazyHost.Value;
        public ServiceClientFactory ServiceClientFactory => Host.Services.GetRequiredService<ServiceClientFactory>();
        public LoggerTelemetryClient TelemetryClient => Host.Services.GetRequiredService<LoggerTelemetryClient>();
        public ILogger Logger => Host.Services.GetRequiredService<ILogger<BaseLogicIntegrationTest>>();
        public IOptions<NuGetInsightsSettings> Options => Host.Services.GetRequiredService<IOptions<NuGetInsightsSettings>>();
        public LimitedConcurrentQueue<string> LogMessages { get; } = new LimitedConcurrentQueue<string>(limit: 1000);

        protected async Task<List<T>> GetEntitiesAsync<T>(string tableName) where T : class, ITableEntity
        {
            var client = await ServiceClientFactory.GetTableServiceClientAsync(Options.Value);
            var table = client.GetTableClient(tableName);
            return await table.QueryAsync<T>().ToListAsync();
        }

        public record DeserializedWideEntity<T>(string PartitionKey, string RowKey, T Entity);

        protected async Task<List<DeserializedWideEntity<T>>> GetWideEntitiesAsync<T>(
            string tableName,
            Func<Stream, T> deserializeEntity = null)
        {
            deserializeEntity ??= stream => MessagePackSerializer.Deserialize<T>(stream, NuGetInsightsMessagePack.Options);

            var service = Host.Services.GetRequiredService<WideEntityService>();

            var wideEntities = await service.RetrieveAsync(tableName);
            var entities = new List<DeserializedWideEntity<T>>();
            foreach (var wideEntity in wideEntities)
            {
                var entity = deserializeEntity(wideEntity.GetStream());
                entities.Add(new DeserializedWideEntity<T>(wideEntity.PartitionKey, wideEntity.RowKey, entity));
            }

            return entities;
        }

        protected async Task AssertBlobCountAsync(string containerName, int expected)
        {
            var serviceClient = await ServiceClientFactory.GetBlobServiceClientAsync(Options.Value);
            var container = serviceClient.GetBlobContainerClient(containerName);
            var blobs = await container.GetBlobsAsync().ToListAsync();
            Assert.Equal(expected, blobs.Count);
        }

        protected async Task<BlobClient> GetBlobAsync(string containerName, int bucket)
        {
            return await GetBlobAsync(containerName, $"compact_{bucket}.csv.gz");
        }

        protected async Task<BlobClient> GetBlobAsync(string containerName, string blobName)
        {
            var serviceClient = await ServiceClientFactory.GetBlobServiceClientAsync(Options.Value);
            var container = serviceClient.GetBlobContainerClient(containerName);
            return container.GetBlobClient(blobName);
        }

        private static readonly ConcurrentDictionary<string, object> StringLock = new ConcurrentDictionary<string, object>();
        private static readonly IReadOnlySet<string> ProjectDirs = new HashSet<string> { "Worker.Test", "Worker.Logic.Test", "Logic.Test" };

        public static string ReadAllTextWithRetry(string testDataFile)
        {
            string expected;
            var attempt = 0;
            while (true)
            {
                try
                {
                    attempt++;
                    expected = File.ReadAllText(testDataFile);
                    break;
                }
                catch (IOException ex) when (ex is not FileNotFoundException && attempt < 5)
                {
                    Thread.Sleep(500 * attempt);
                }
            }

            return expected;
        }

        protected static void OverwriteTestDataAndCopyToSource(string testDataFile, string actual)
        {
            var sourcePath = Path.GetFullPath(testDataFile);
            var projectDir = sourcePath
                .Split(Path.DirectorySeparatorChar)
                .Reverse()
                .First(ProjectDirs.Contains);
            var repoDir = DirectoryHelper.GetRepositoryRoot();
            var destPath = Path.Combine(repoDir, "test", projectDir, testDataFile);

            lock (StringLock.GetOrAdd(sourcePath, _ => new object()))
                lock (StringLock.GetOrAdd(destPath, _ => new object()))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(testDataFile));
                    File.WriteAllText(testDataFile, actual);

                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                    File.Copy(sourcePath, destPath, overwrite: true);
                }
        }

        public virtual Task InitializeAsync()
        {
            var ex = LogicTestSettings.StorageConnectionError.Value;
            if (ex is not null)
            {
                throw ex;
            }

            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            Output.WriteTestCleanup();

            // Remove test hooks for the HTTP client pipeline to allow normal clean-up.
            HttpMessageHandlerFactory.OnSendAsync = null;

            try
            {
                await DisposeInternalAsync();

                AssertLogLevelOrLess();
            }
            finally
            {
                try
                {
                    if (!LogicTestSettings.HasStorageConnectionError)
                    {
                        await CleanUpStorageContainers(x => x.StartsWith(StoragePrefix, StringComparison.Ordinal));
                    }
                }
                finally
                {
                    if (_lazyHost.IsValueCreated)
                    {
                        _lazyHost.Value.Dispose();
                    }
                }
            }
        }

        protected virtual Task DisposeInternalAsync()
        {
            return Task.CompletedTask;
        }

        protected async Task CleanUpStorageContainers(Predicate<string> shouldDelete)
        {
            var serviceClient = await ServiceClientFactory.GetBlobServiceClientAsync(Options.Value);
            var containerItems = await serviceClient.GetBlobContainersAsync().ToListAsync();
            foreach (var containerItem in containerItems.Where(x => shouldDelete(x.Name)))
            {
                Logger.LogInformation("Deleting blob container: {Name}", containerItem.Name);
                try
                {
                    await serviceClient.DeleteBlobContainerAsync(containerItem.Name);
                }
                catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
                {
                    // The delete is already done. This can happen if there are internal retries for the deletion.
                }
            }

            var queueServiceClient = await ServiceClientFactory.GetQueueServiceClientAsync(Options.Value);
            var queueItems = await queueServiceClient.GetQueuesAsync().ToListAsync();
            foreach (var queueItem in queueItems.Where(x => shouldDelete(x.Name)))
            {
                Logger.LogInformation("Deleting storage queue: {Name}", queueItem.Name);
                try
                {
                    await queueServiceClient.DeleteQueueAsync(queueItem.Name);
                }
                catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
                {
                    // The delete is already done. This can happen if there are internal retries for the deletion.
                }
            }

            var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync(Options.Value);
            var tableItems = await tableServiceClient.QueryAsync().ToListAsync();
            foreach (var tableItem in tableItems.Where(x => shouldDelete(x.Name)))
            {
                Logger.LogInformation("Deleting storage table: {Name}", tableItem.Name);
                try
                {
                    await tableServiceClient.DeleteTableAsync(tableItem.Name);
                }
                catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
                {
                    // The delete is already done. This can happen if there are internal retries for the deletion.
                }
            }
        }

        private void AssertLogLevelOrLess()
        {
            var logMessages = LogLevelToCount
                .Where(x => x.Key >= AssertLogLevel)
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Key)
                .ToList();
            foreach ((var logLevel, var count) in logMessages)
            {
                Logger.LogInformation("There were {Count} {LogLevel} log messages.", count, logLevel);
            }
            Assert.Empty(logMessages);
        }

        public static HttpRequestMessage Clone(HttpRequestMessage req)
        {
            var clone = new HttpRequestMessage(req.Method, req.RequestUri)
            {
                Content = req.Content,
                Version = req.Version
            };

            foreach (var header in req.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}

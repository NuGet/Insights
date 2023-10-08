// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public abstract class BaseLogicIntegrationTest : IClassFixture<DefaultWebApplicationFactory<StaticFilesStartup>>, IAsyncLifetime
    {
        static BaseLogicIntegrationTest()
        {
            var oldTemp = Environment.GetEnvironmentVariable("TEMP");
            var newTemp = Path.GetFullPath(Path.Join(oldTemp, "NuGet.Insights.Temp"));
            Directory.CreateDirectory(newTemp);
            Environment.SetEnvironmentVariable("TEMP", newTemp);
            Environment.SetEnvironmentVariable("TMP", newTemp);
        }

        public const string ProgramName = "NuGet.Insights.Logic.Test";
        public const string TestData = "TestData";
        public const string Step1 = "Step1";
        public const string Step2 = "Step2";
        public const string Step3 = "Step3";

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
            StoragePrefix = TestSettings.NewStoragePrefix();
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
                    serviceCollection.AddNuGetInsights(ProgramName);

                    serviceCollection.AddSingleton((INuGetInsightsHttpMessageHandlerFactory)HttpMessageHandlerFactory);

                    serviceCollection.AddTransient<ITelemetryClient>(s => new LoggerTelemetryClient(s.GetRequiredService<ILogger<LoggerTelemetryClient>>()));

                    serviceCollection.AddLogging(o =>
                    {
                        o.SetMinimumLevel(LogLevel.Trace);
                        o.AddProvider(new XunitLoggerProvider(
                            output,
                            LogLevel.Trace,
                            LogLevelToCount,
                            TransformLogLevel,
                            FailFastLogLevel, LogMessages));
                    });

                    serviceCollection.Configure((Action<NuGetInsightsSettings>)ConfigureDefaultsAndSettings);
                });

            ConfigureHostBuilder(hostBuilder);

            return hostBuilder.Build();
        }

        protected LogLevel FailFastLogLevel { get; set; } = LogLevel.Error;
        protected LogLevel AssertLogLevel { get; set; } = LogLevel.Warning;
        public Func<LogLevel, string, LogLevel> TransformLogLevel { get; set; } = (LogLevel logLevel, string message) =>
        {
            if (message.StartsWith(LoggerExtensions.TransientPrefix) && logLevel == LogLevel.Warning)
            {
                return LogLevel.Information;
            }

            return logLevel;
        };

        protected virtual void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
        }

        protected void ConfigureDefaultsAndSettings(NuGetInsightsSettings x)
        {
            x.StorageConnectionString = TestSettings.StorageConnectionString;
            x.StorageBlobReadSharedAccessSignature = TestSettings.StorageBlobReadSharedAccessSignature;

            x.DownloadsV1AgeLimit = TimeSpan.MaxValue;
            x.OwnersV2AgeLimit = TimeSpan.MaxValue;
            x.VerifiedPackagesV1AgeLimit = TimeSpan.MaxValue;

            x.LeaseContainerName = $"{StoragePrefix}1l1";
            x.PackageArchiveTableName = $"{StoragePrefix}1pa1";
            x.SymbolPackageArchiveTableName = $"{StoragePrefix}1sa1";
            x.PackageManifestTableName = $"{StoragePrefix}1pm1";
            x.PackageReadmeTableName = $"{StoragePrefix}1prm1";
            x.PackageHashesTableName = $"{StoragePrefix}1ph1";
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
        public Action<NuGetInsightsSettings> ConfigureSettings { get; set; }
        public IHost Host => _lazyHost.Value;
        public ServiceClientFactory ServiceClientFactory => Host.Services.GetRequiredService<ServiceClientFactory>();
        public ITelemetryClient TelemetryClient => Host.Services.GetRequiredService<ITelemetryClient>();
        public ILogger Logger => Host.Services.GetRequiredService<ILogger<BaseLogicIntegrationTest>>();
        public ConcurrentQueue<string> LogMessages { get; } = new ConcurrentQueue<string>();

        protected async Task AssertBlobCountAsync(string containerName, int expected)
        {
            var client = await ServiceClientFactory.GetBlobServiceClientAsync();
            var container = client.GetBlobContainerClient(containerName);
            var blobs = await container.GetBlobsAsync().ToListAsync();
            Assert.Equal(expected, blobs.Count);
        }

        protected async Task AssertCsvBlobAsync<T>(string containerName, string testName, string stepName, string fileName, string blobName) where T : ICsvRecord
        {
            Assert.EndsWith(".csv.gz", blobName);
            var actual = await AssertBlobAsync(containerName, testName, stepName, fileName, blobName, gzip: true);
            var headerFactory = Activator.CreateInstance<T>();
            var stringWriter = new StringWriter { NewLine = "\n" };
            headerFactory.WriteHeader(stringWriter);
            Assert.StartsWith(stringWriter.ToString(), actual);
        }

        protected async Task<BlobClient> GetBlobAsync(string containerName, string blobName)
        {
            var client = await ServiceClientFactory.GetBlobServiceClientAsync();
            var container = client.GetBlobContainerClient(containerName);
            return container.GetBlobClient(blobName);
        }

        protected async Task<string> AssertBlobAsync(string containerName, string testName, string stepName, string fileName, string blobName, bool gzip = false)
        {
            var blob = await GetBlobAsync(containerName, blobName);

            string actual;
            if (gzip)
            {
                if (fileName == null)
                {
                    fileName = blobName.Substring(0, blobName.Length - ".gz".Length);
                }

                Assert.EndsWith(".gz", blobName);

                using var destStream = new MemoryStream();
                using BlobDownloadInfo downloadInfo = await blob.DownloadAsync();
                await downloadInfo.Content.CopyToAsync(destStream);
                destStream.Position = 0;

                Assert.Contains(StorageUtility.RawSizeBytesMetadata, downloadInfo.Details.Metadata);
                var uncompressedLength = long.Parse(downloadInfo.Details.Metadata[StorageUtility.RawSizeBytesMetadata]);

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
                if (fileName == null)
                {
                    fileName = blobName;
                }

                using BlobDownloadInfo downloadInfo = await blob.DownloadAsync();
                using var reader = new StreamReader(downloadInfo.Content);
                actual = await reader.ReadToEndAsync();
            }

            var testDataFile = Path.Combine(TestData, testName, stepName, fileName);
            if (OverwriteTestData)
            {
                OverwriteTestDataAndCopyToSource(testDataFile, actual);
            }
            var expected = File.ReadAllText(testDataFile);
            Assert.Equal(expected, actual);

            return actual;
        }

        private static readonly ConcurrentDictionary<string, object> StringLock = new ConcurrentDictionary<string, object>();

        protected static void OverwriteTestDataAndCopyToSource(string testDataFile, string actual)
        {
            var sourcePath = Path.GetFullPath(testDataFile);
            var projectDir = sourcePath.Contains("Worker.Logic.Test") ? "Worker.Logic.Test" : "Logic.Test";
            var repoDir = TestSettings.GetRepositoryRoot();
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

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public virtual async Task DisposeAsync()
        {
            Output.WriteLine(new string('-', 80));
            Output.WriteLine("Beginning test clean-up.");
            Output.WriteLine(new string('-', 80));

            try
            {
                // Global assertions
                AssertLogLevelOrLess();
            }
            finally
            {
                await CleanUpStorageContainers(x => x.StartsWith(StoragePrefix));
            }
        }

        protected async Task CleanUpStorageContainers(Predicate<string> shouldDelete)
        {
            // Remove test hooks ffor the HTTP client pipeline to allow normal clean-up.
            HttpMessageHandlerFactory.OnSendAsync = null;

            var blobServiceClient = await ServiceClientFactory.GetBlobServiceClientAsync();
            var containerItems = await blobServiceClient.GetBlobContainersAsync().ToListAsync();
            foreach (var containerItem in containerItems.Where(x => shouldDelete(x.Name)))
            {
                Logger.LogInformation("Deleting blob container: {Name}", containerItem.Name);
                try
                {
                    await blobServiceClient.DeleteBlobContainerAsync(containerItem.Name);
                }
                catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
                {
                    // The delete is already done. This can happen if there are internal retries for the deletion.
                }
            }

            var queueServiceClient = await ServiceClientFactory.GetQueueServiceClientAsync();
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

            var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
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

        public static string SerializeTestJson(object obj)
        {
            var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters =
                {
                    new JsonStringEnumConverter(),
                },
            });

            return json.Replace("\r\n", "\n");
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

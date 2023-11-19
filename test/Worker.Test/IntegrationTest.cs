// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using Kusto.Ingest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.Workflow;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker
{
    public class IntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public IntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
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

                    serviceCollection.Configure((Action<NuGetInsightsSettings>)ConfigureDefaultsAndSettings);
                    serviceCollection.Configure((Action<NuGetInsightsWorkerSettings>)ConfigureWorkerDefaultsAndSettings);
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

        public class CanRunTimersAsync : IntegrationTest
        {
            public CanRunTimersAsync(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
                SetupDefaultMockVersionSet();
            }

            protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
            {
                base.ConfigureHostBuilder(hostBuilder);

                hostBuilder.ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddTransient(s => MockVersionSetProvider.Object);
                });
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                ConfigureSettings = x =>
                {
                    x.DownloadsV1Urls = new List<string> { $"http://localhost/{TestData}/DownloadsToCsv/{Step1}/downloads.v1.json" };
                    x.OwnersV2Urls = new List<string> { $"http://localhost/{TestData}/OwnersToCsv/{Step1}/owners.v2.json" };
                    x.VerifiedPackagesV1Urls = new List<string> { $"http://localhost/{TestData}/VerifiedPackagesToCsv/{Step1}/verifiedPackages.json" };
                };
                ConfigureWorkerSettings = x =>
                {
                    x.AutoStartDownloadToCsv = true;
                    x.AutoStartOwnersToCsv = true;
                    x.AutoStartVerifiedPackagesToCsv = true;
                };

                HttpMessageHandlerFactory.OnSendAsync = async (req, _, _) =>
                {
                    if (Options.Value.DownloadsV1Urls.Contains(req.RequestUri.AbsoluteUri)
                        || Options.Value.OwnersV2Urls.Contains(req.RequestUri.AbsoluteUri)
                        || Options.Value.VerifiedPackagesV1Urls.Contains(req.RequestUri.AbsoluteUri))
                    {
                        return await TestDataHttpClient.SendAsync(Clone(req));
                    }

                    return null;
                };

                var service = Host.Services.GetRequiredService<TimerExecutionService>();
                await service.InitializeAsync();

                // Act
                using (var scope = Host.Services.CreateScope())
                {
                    await scope
                        .ServiceProvider
                        .GetRequiredService<Functions>()
                        .TimerAsync(timerInfo: null);
                }

                await ProcessQueueAsync(() => Task.FromResult(true));

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageDownloadContainerName, 1);
                await AssertBlobCountAsync(Options.Value.PackageOwnerContainerName, 1);
                await AssertBlobCountAsync(Options.Value.VerifiedPackageContainerName, 1);
            }
        }

        public class AllOutputCanBeReset : IntegrationTest
        {
            public AllOutputCanBeReset(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Initialize all drivers and timers
                var driverFactory = Host.Services.GetRequiredService<ICatalogScanDriverFactory>();
                foreach (var driverType in CatalogScanCursorService.StartableDriverTypes)
                {
                    var driver = driverFactory.Create(driverType);
                    var descendingId = StorageUtility.GenerateDescendingId();
                    var catalogIndexScan = new CatalogIndexScan(
                        driverType,
                        descendingId.ToString(),
                        descendingId.Unique);
                    await driver.InitializeAsync(catalogIndexScan);
                    await driver.FinalizeAsync(catalogIndexScan);
                }

                var timers = Host.Services.GetServices<ITimer>();
                foreach (var timer in timers)
                {
                    await timer.InitializeAsync();
                }

                // Get all table names and blob storage container names in the configuration
                var options = Options.Value;
                var properties = options.GetType().GetProperties();
                SortedDictionary<string, string> GetNames(IEnumerable<PropertyInfo> properties)
                {
                    return new SortedDictionary<string, string>(
                        properties.ToDictionary(x => x.Name, x => (string)x.GetValue(options)),
                        StringComparer.Ordinal);
                }
                var tables = GetNames(properties.Where(x => x.Name.EndsWith("TableName", StringComparison.Ordinal)));
                var blobContainers = GetNames(properties.Where(x => x.Name.EndsWith("ContainerName", StringComparison.Ordinal)));

                // Remove transient tables
                tables.Remove(nameof(NuGetInsightsWorkerSettings.CatalogLeafScanTableName));
                tables.Remove(nameof(NuGetInsightsWorkerSettings.CatalogPageScanTableName));
                tables.Remove(nameof(NuGetInsightsWorkerSettings.CsvRecordTableName));
                tables.Remove(nameof(NuGetInsightsWorkerSettings.VersionSetAggregateTableName));

                // Remove tables and containers for unsupported drivers
#if !ENABLE_CRYPTOAPI
                tables.Remove(nameof(NuGetInsightsWorkerSettings.CertificateToPackageTableName));
                tables.Remove(nameof(NuGetInsightsWorkerSettings.PackageToCertificateTableName));
                blobContainers.Remove(nameof(NuGetInsightsWorkerSettings.CertificateContainerName));
                blobContainers.Remove(nameof(NuGetInsightsWorkerSettings.PackageCertificateContainerName));
#endif

#if !ENABLE_NPE
                blobContainers.Remove(nameof(NuGetInsightsWorkerSettings.NuGetPackageExplorerContainerName));
                blobContainers.Remove(nameof(NuGetInsightsWorkerSettings.NuGetPackageExplorerFileContainerName));
#endif

                // Verify all table and blob storage container names are created
                var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
                foreach ((var key, var tableName) in tables)
                {
                    var table = tableServiceClient.GetTableClient(tableName);
                    Assert.True(await table.ExistsAsync(), $"The table for {key} ('{tableName}') should have been created.");
                }

                var blobServiceClient = await ServiceClientFactory.GetBlobServiceClientAsync();
                foreach ((var key, var containerName) in blobContainers)
                {
                    var container = blobServiceClient.GetBlobContainerClient(containerName);
                    Assert.True(await container.ExistsAsync(), $"The blob container for {key} ('{containerName}') should have been created.");
                }

                // Destroy output for all drivers and timers
                foreach (var driverType in CatalogScanCursorService.StartableDriverTypes)
                {
                    var driver = driverFactory.Create(driverType);
                    await driver.DestroyOutputAsync();
                }

                foreach (var timer in timers)
                {
                    if (timer.CanDestroy)
                    {
                        await timer.DestroyAsync();
                    }
                }

                // Remove non-output tables
                tables.Remove(nameof(NuGetInsightsWorkerSettings.CatalogIndexScanTableName));
                tables.Remove(nameof(NuGetInsightsWorkerSettings.CursorTableName));
                tables.Remove(nameof(NuGetInsightsWorkerSettings.KustoIngestionTableName));
                tables.Remove(nameof(NuGetInsightsWorkerSettings.TaskStateTableName));
                tables.Remove(nameof(NuGetInsightsWorkerSettings.TimerTableName));
                tables.Remove(nameof(NuGetInsightsWorkerSettings.WorkflowRunTableName));

                // Remove non-output containers
                blobContainers.Remove(nameof(NuGetInsightsWorkerSettings.LeaseContainerName));

                // Verify all out table and blob storage containers are deleted
                foreach ((var key, var tableName) in tables)
                {
                    var table = tableServiceClient.GetTableClient(tableName);
                    Assert.True(await WaitForAsync(async () => !await table.ExistsAsync()), $"The table for {key} ('{tableName}') should have been deleted.");
                }

                foreach ((var key, var containerName) in blobContainers)
                {
                    var container = blobServiceClient.GetBlobContainerClient(containerName);
                    Assert.True(await WaitForAsync(async () => !await container.ExistsAsync()), $"The blob container for {key} ('{containerName}') should have been deleted.");
                }
            }

            private static async Task<bool> WaitForAsync(Func<Task<bool>> isCompleteAsync)
            {
                var sw = Stopwatch.StartNew();
                bool complete;
                while (!(complete = await isCompleteAsync()) && sw.Elapsed < TimeSpan.FromSeconds(5 * 60))
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                return complete;
            }
        }

        public class ExecutesEntireWorkflow : IntegrationTest
        {
            public ExecutesEntireWorkflow(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
            {
                base.ConfigureHostBuilder(hostBuilder);

                hostBuilder.ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddTransient(x => MockCslAdminProvider.Object);
                    serviceCollection.AddTransient(x => MockKustoQueueIngestClient.Object);
                    serviceCollection.AddTransient(x => MockCslQueryProvider.Object);
                });
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                ConfigureSettings = x =>
                {
                    x.MaxTempMemoryStreamSize = 0;
                    x.TempDirectories[0].MaxConcurrentWriters = 1;
                    x.DownloadsV1Urls = new List<string> { $"http://localhost/{TestData}/DownloadsToCsv/downloads.v1.json" };
                    x.OwnersV2Urls = new List<string> { $"http://localhost/{TestData}/OwnersToCsv/owners.v2.json" };
                    x.VerifiedPackagesV1Urls = new List<string> { $"http://localhost/{TestData}/VerifiedPackagesToCsv/verifiedPackages.json" };
                };
                ConfigureWorkerSettings = x =>
                {
                    x.AppendResultStorageBucketCount = 1;
                    x.KustoConnectionString = "fake connection string";
                    x.KustoDatabaseName = "fake database name";
                };

                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);

                HttpMessageHandlerFactory.OnSendAsync = async (req, _, _) =>
                {
                    if (req.RequestUri.AbsoluteUri == "https://api.nuget.org/v3-flatcontainer/cursor.json")
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(@$"{{""value"":""{max1:O}""}}")
                        };
                    }

                    if (req.RequestUri.AbsolutePath.EndsWith("/downloads.v1.json", StringComparison.Ordinal))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/DownloadsToCsv/{Step1}/downloads.v1.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    if (req.RequestUri.AbsolutePath.EndsWith("/owners.v2.json", StringComparison.Ordinal))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/OwnersToCsv/{Step1}/owners.v2.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    if (req.RequestUri.AbsolutePath.EndsWith("/verifiedPackages.json", StringComparison.Ordinal))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/VerifiedPackagesToCsv/{Step1}/verifiedPackages.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };

                await WorkflowService.InitializeAsync();

                foreach (var type in CatalogScanCursorService.StartableDriverTypes)
                {
                    await SetCursorAsync(type, min0);
                }

                // Act
                var run = await WorkflowService.StartAsync();
                Assert.NotNull(run);
                var attempts = 0;
                await ProcessQueueAsync(
                    async () =>
                    {
                        if (!await WorkflowService.IsAnyWorkflowStepRunningAsync())
                        {
                            return true;
                        }

                        attempts++;
                        if (attempts > 60)
                        {
                            return true;
                        }

                        await Task.Delay(1000);

                        return false;
                    },
                    workerCount: 8);

                // Assert
                // Make sure all scans completed.
                var indexScans = await CatalogScanStorageService.GetIndexScansAsync();
                Assert.All(indexScans, x => Assert.Equal(CatalogIndexScanState.Complete, x.State));
                Assert.Equal(
                    CatalogScanCursorService.StartableDriverTypes.ToArray(),
                    indexScans.Select(x => x.DriverType).Order().ToArray());

                // Make sure all of the containers are have ingestions
                var containerNames = Host.Services.GetRequiredService<CsvResultStorageContainers>().GetContainerNames();
                foreach (var containerName in containerNames)
                {
                    MockKustoQueueIngestClient.Verify(x => x.IngestFromStorageAsync(
                        It.Is<string>(y => y.Contains(containerName)),
                        It.IsAny<KustoIngestionProperties>(),
                        It.IsAny<StorageSourceOptions>()));
                }

                // Make sure no workflow step is running.
                Assert.False(await WorkflowService.IsAnyWorkflowStepRunningAsync());
            }
        }

        public class DownloadsNupkgsAndNuspecsInTheExpectedDrivers : IntegrationTest
        {
            public DownloadsNupkgsAndNuspecsInTheExpectedDrivers(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                ConfigureSettings = x =>
                {
                    x.LegacyReadmeUrlPattern = "https://api.nuget.org/legacy-readmes/{0}/{1}/README.md"; // fake
                    x.MaxTempMemoryStreamSize = 0;
                    x.TempDirectories[0].MaxConcurrentWriters = 1;
                };
                ConfigureWorkerSettings = x =>
                {
                    x.AppendResultStorageBucketCount = 1;
                };

                await CatalogScanService.InitializeAsync();

                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);

                foreach (var type in CatalogScanCursorService.StartableDriverTypes)
                {
                    await SetCursorAsync(type, min0);
                }

                // Act

                // Load the manifests
                var loadPackageManifest = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageManifest, max1);
                await UpdateAsync(loadPackageManifest.Scan);

                var startingNuspecRequestCount = GetNuspecRequestCount();

                // Load the readmes
                var loadPackageReadme = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageReadme, max1);
                await UpdateAsync(loadPackageReadme.Scan);

                var startingReadmeRequestCount = GetReadmeRequestCount();

                // Load latest package leaves
                var loadLatestPackageLeaf = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadLatestPackageLeaf, max1);
                await UpdateAsync(loadLatestPackageLeaf.Scan);

                Assert.Equal(0, GetNupkgRequestCount());

                // Load the symbol packages
                var loadSymbolPackageArchive = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadSymbolPackageArchive, max1);
                await UpdateAsync(loadSymbolPackageArchive.Scan);

                var startingSnupkgRequestCount = GetSnupkgRequestCount();

                // Load the packages, process package assemblies, and run NuGet Package Explorer.
                var loadPackageArchive = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageArchive, max1);
                await UpdateAsync(loadPackageArchive.Scan);
                var packageAssemblyToCsv = await CatalogScanService.UpdateAsync(CatalogScanDriverType.PackageAssemblyToCsv, max1);
                var packageContentToCsv = await CatalogScanService.UpdateAsync(CatalogScanDriverType.PackageContentToCsv, max1);
#if ENABLE_NPE
                var nuGetPackageExplorerToCsv = await CatalogScanService.UpdateAsync(CatalogScanDriverType.NuGetPackageExplorerToCsv, max1);
#endif
                await UpdateAsync(packageAssemblyToCsv.Scan);
                await UpdateAsync(packageContentToCsv.Scan);
#if ENABLE_NPE
                await UpdateAsync(nuGetPackageExplorerToCsv.Scan);
#endif

                var startingNupkgRequestCount = GetNupkgRequestCount();
                var intermediateSnupkgRequestCount = GetSnupkgRequestCount();

                // Load the versions
                var loadPackageVersion = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageVersion, max1);
                await UpdateAsync(loadPackageVersion.Scan);

                // Start all of the scans
                var startedScans = new List<CatalogIndexScan>();
                foreach (var type in CatalogScanCursorService.StartableDriverTypes)
                {
                    var startedScan = await CatalogScanService.UpdateAsync(type, max1);
                    if (startedScan.Type == CatalogScanServiceResultType.FullyCaughtUpWithMax)
                    {
                        continue;
                    }
                    Assert.Equal(CatalogScanServiceResultType.NewStarted, startedScan.Type);
                    startedScans.Add(startedScan.Scan);
                }

                // Wait for all of the scans to complete.
                foreach (var scan in startedScans)
                {
                    await UpdateAsync(scan);
                }

                var finalNupkgRequestCount = GetNupkgRequestCount();
                var finalNuspecRequestCount = GetNuspecRequestCount();
                var finalReadmeRequestCount = GetReadmeRequestCount();
                var finalSnupkgRequestCount = GetSnupkgRequestCount();

                // Assert
                var rawMessageEnqueuer = Host.Services.GetRequiredService<IRawMessageEnqueuer>();
                foreach (var queue in Enum.GetValues(typeof(QueueType)).Cast<QueueType>())
                {
                    Assert.Equal(0, await rawMessageEnqueuer.GetApproximateMessageCountAsync(queue));
                    Assert.Equal(0, await rawMessageEnqueuer.GetAvailableMessageCountLowerBoundAsync(queue, 32));
                    Assert.Equal(0, await rawMessageEnqueuer.GetPoisonApproximateMessageCountAsync(queue));
                    Assert.Equal(0, await rawMessageEnqueuer.GetPoisonAvailableMessageCountLowerBoundAsync(queue, 32));
                }

                Assert.NotEqual(0, startingNupkgRequestCount);
                Assert.NotEqual(0, startingNuspecRequestCount);
                Assert.NotEqual(0, startingReadmeRequestCount);
                Assert.NotEqual(0, startingSnupkgRequestCount);
#if ENABLE_NPE
                Assert.NotEqual(startingSnupkgRequestCount, intermediateSnupkgRequestCount);
#else
                Assert.Equal(startingSnupkgRequestCount, intermediateSnupkgRequestCount);
#endif
                Assert.Equal(startingNupkgRequestCount, finalNupkgRequestCount);
                Assert.Equal(startingNuspecRequestCount, finalNuspecRequestCount);
                Assert.Equal(startingReadmeRequestCount, finalReadmeRequestCount);
                Assert.Equal(intermediateSnupkgRequestCount, finalSnupkgRequestCount);

                var userAgents = HttpMessageHandlerFactory.Responses.Select(r => r.RequestMessage.Headers.UserAgent.ToString()).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
                foreach (var userAgent in userAgents)
                {
                    Logger.LogInformation("Found User-Agent: {UserAgent}", userAgent);
                }

                Assert.Equal(4, userAgents.Count); // NuGet Insights, and Blob + Queue + Table Azure SDK.
                Assert.StartsWith("Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)", userAgents[0], StringComparison.Ordinal);
                Assert.Matches(@"(NuGet Test Client)/?(\d+)?\.?(\d+)?\.?(\d+)?", userAgents[0]);
                Assert.StartsWith("azsdk-net-Data.Tables/", userAgents[1], StringComparison.Ordinal);
                Assert.StartsWith("azsdk-net-Storage.Blobs/", userAgents[2], StringComparison.Ordinal);
                Assert.StartsWith("azsdk-net-Storage.Queues/", userAgents[3], StringComparison.Ordinal);
            }
        }

        private int GetNuspecRequestCount()
        {
            return HttpMessageHandlerFactory.Responses.Count(x => x.RequestMessage.RequestUri.AbsoluteUri.EndsWith(".nuspec", StringComparison.Ordinal));
        }

        private int GetNupkgRequestCount()
        {
            return HttpMessageHandlerFactory.Responses.Count(x => x.RequestMessage.RequestUri.AbsoluteUri.EndsWith(".nupkg", StringComparison.Ordinal));
        }

        private int GetReadmeRequestCount()
        {
            return HttpMessageHandlerFactory.Responses.Count(x => x.RequestMessage.RequestUri.AbsoluteUri.EndsWith(".md", StringComparison.Ordinal));
        }

        private int GetSnupkgRequestCount()
        {
            return HttpMessageHandlerFactory.Responses.Count(x => x.RequestMessage.RequestUri.AbsoluteUri.EndsWith(".snupkg", StringComparison.Ordinal));
        }
    }
}

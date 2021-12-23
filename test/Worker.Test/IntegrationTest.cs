// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using Kusto.Ingest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.DownloadsToCsv;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.OwnersToCsv;
using NuGet.Insights.Worker.VerifiedPackagesToCsv;
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
                .ConfigureWebJobs(new Startup().Configure)
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

        public class TimersAreInProperOrder : IntegrationTest
        {
            public TimersAreInProperOrder(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public void Execute()
            {
                var expected = new List<List<Type>>
                {
                    new List<Type>
                    {
                        typeof(MetricsTimer),
                        typeof(WorkflowTimer),
                    },
                    new List<Type>
                    {
                        typeof(CatalogScanUpdateTimer),
                    },
                    new List<Type>
                    {
                        typeof(AuxiliaryFileUpdaterTimer<AsOfData<PackageDownloads>>),
                        typeof(AuxiliaryFileUpdaterTimer<AsOfData<PackageOwner>>),
                        typeof(AuxiliaryFileUpdaterTimer<AsOfData<VerifiedPackage>>),
                    },
                    new List<Type>
                    {
                        typeof(KustoIngestionTimer),
                    },
                };
                var actual = Host
                    .Services
                    .GetRequiredService<IEnumerable<ITimer>>()
                    .GroupBy(x => x.Order)
                    .OrderBy(x => x.Key)
                    .Select(x => x.OrderBy(x => x.Name).Select(x => x.GetType()).ToList())
                    .ToList();

                Assert.Equal(expected.Count, actual.Count);
                for (var i = 0; i < expected.Count; i++)
                {
                    Assert.Equal(expected[i], actual[i]);
                }
            }
        }

        public class CanRunTimersAsync : IntegrationTest
        {
            public CanRunTimersAsync(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
                MockVersionSet.SetReturnsDefault(true);
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
                ConfigureSettings = x =>
                {
                    x.DownloadsV1Url = $"http://localhost/{TestData}/DownloadsToCsv/{Step1}/downloads.v1.json";
                    x.OwnersV2Url = $"http://localhost/{TestData}/OwnersToCsv/{Step1}/owners.v2.json";
                    x.VerifiedPackagesV1Url = $"http://localhost/{TestData}/VerifiedPackagesToCsv/{Step1}/verifiedPackages.json";
                };
                ConfigureWorkerSettings = x =>
                {
                    x.AutoStartDownloadToCsv = true;
                    x.AutoStartOwnersToCsv = true;
                    x.AutoStartVerifiedPackagesToCsv = true;
                };

                // Arrange
                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsoluteUri == Options.Value.DownloadsV1Url
                     || req.RequestUri.AbsoluteUri == Options.Value.OwnersV2Url
                     || req.RequestUri.AbsoluteUri == Options.Value.VerifiedPackagesV1Url)
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

                await ProcessQueueAsync(() => { }, () => Task.FromResult(true));

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageDownloadContainerName, 1);
                await AssertBlobCountAsync(Options.Value.PackageOwnerContainerName, 1);
                await AssertBlobCountAsync(Options.Value.VerifiedPackageContainerName, 1);
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
                });
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureSettings = x =>
                {
                    x.MaxTempMemoryStreamSize = 0;
                    x.TempDirectories[0].MaxConcurrentWriters = 1;
                    x.DownloadsV1Url = $"http://localhost/{TestData}/DownloadsToCsv/downloads.v1.json";
                    x.OwnersV2Url = $"http://localhost/{TestData}/OwnersToCsv/owners.v2.json";
                    x.VerifiedPackagesV1Url = $"http://localhost/{TestData}/VerifiedPackagesToCsv/verifiedPackages.json";
                };
                ConfigureWorkerSettings = x =>
                {
                    x.AppendResultStorageBucketCount = 1;
                };
                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsolutePath.EndsWith("/downloads.v1.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/DownloadsToCsv/{Step1}/downloads.v1.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    if (req.RequestUri.AbsolutePath.EndsWith("/owners.v2.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/OwnersToCsv/{Step1}/owners.v2.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    if (req.RequestUri.AbsolutePath.EndsWith("/verifiedPackages.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/VerifiedPackagesToCsv/{Step1}/verifiedPackages.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };

                // Arrange
                await CatalogScanService.InitializeAsync();
                await WorkflowService.InitializeAsync();

                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");

                foreach (var type in CatalogScanCursorService.StartableDriverTypes)
                {
                    await SetCursorAsync(type, min0);
                }

                // Act
                var run = await WorkflowService.StartAsync(max1);
                Assert.NotNull(run);
                var attempts = 0;
                await ProcessQueueAsync(
                    () => { },
                    async () =>
                    {
                        if (!await WorkflowService.IsAnyWorkflowStepRunningAsync())
                        {
                            return true;
                        }

                        attempts++;
                        if (attempts > 30)
                        {
                            return true;
                        }

                        await Task.Delay(1000);

                        return false;
                    });

                // Assert
                // Make sure all scans completed.
                var indexScans = await CatalogScanStorageService.GetIndexScansAsync();
                Assert.All(indexScans, x => Assert.Equal(CatalogIndexScanState.Complete, x.State));
                Assert.Equal(
                    CatalogScanCursorService.StartableDriverTypes.ToArray(),
                    indexScans.Select(x => x.DriverType).OrderBy(x => x).ToArray());

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
                ConfigureSettings = x =>
                {
                    x.MaxTempMemoryStreamSize = 0;
                    x.TempDirectories[0].MaxConcurrentWriters = 1;
                };
                ConfigureWorkerSettings = x =>
                {
                    x.AppendResultStorageBucketCount = 1;
                };

                // Arrange
                await CatalogScanService.InitializeAsync();

                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");

                foreach (var type in CatalogScanCursorService.StartableDriverTypes)
                {
                    await SetCursorAsync(type, min0);
                }

                // Act

                // Load the manifests
                var loadPackageManifest = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageManifest, max1);
                await UpdateAsync(loadPackageManifest.Scan);

                var startingNuspecRequestCount = GetNuspecRequestCount();

                // Load latest package leaves
                var loadLatestPackageLeaf = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadLatestPackageLeaf, max1);
                await UpdateAsync(loadLatestPackageLeaf.Scan);

                Assert.Equal(0, GetNupkgRequestCount());

                // Load the packages, process package assemblies, and run NuGet Package Explorer.
                var loadPackageArchive = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageArchive, max1);
                await UpdateAsync(loadPackageArchive.Scan);
                var packageAssemblyToCsv = await CatalogScanService.UpdateAsync(CatalogScanDriverType.PackageAssemblyToCsv, max1);
#if ENABLE_NPE
                var nuGetPackageExplorerToCsv = await CatalogScanService.UpdateAsync(CatalogScanDriverType.NuGetPackageExplorerToCsv, max1);
#endif
                await UpdateAsync(packageAssemblyToCsv.Scan);
#if ENABLE_NPE
                await UpdateAsync(nuGetPackageExplorerToCsv.Scan);
#endif

                var startingNupkgRequestCount = GetNupkgRequestCount();

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
                Assert.Equal(startingNupkgRequestCount, finalNupkgRequestCount);
                Assert.Equal(startingNuspecRequestCount, finalNuspecRequestCount);

                var userAgents = HttpMessageHandlerFactory.Requests.Select(r => r.Headers.UserAgent.ToString()).Distinct();
                var userAgent = Assert.Single(userAgents);
                Assert.StartsWith("Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)", userAgent);
                Assert.Matches(@"(NuGet Test Client)/?(\d+)?\.?(\d+)?\.?(\d+)?", userAgent);
            }
        }

        private int GetNuspecRequestCount()
        {
            return HttpMessageHandlerFactory.Requests.Count(x => x.RequestUri.AbsoluteUri.EndsWith(".nuspec"));
        }

        private int GetNupkgRequestCount()
        {
            return HttpMessageHandlerFactory.Requests.Count(x => x.RequestUri.AbsoluteUri.EndsWith(".nupkg"));
        }
    }
}

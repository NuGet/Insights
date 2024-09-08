// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Kusto.Ingest;
using Microsoft.Extensions.Hosting;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Worker
{
    public class EntireWorkflowSucceedsWithDefaultSettings : EntireWorkflowSucceeds
    {
        public EntireWorkflowSucceedsWithDefaultSettings(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public async Task Execute()
        {
            await ExecuteInternalAsync();
        }
    }

    public class EntireWorkflowSucceedsWithHeavyDiskSettings : EntireWorkflowSucceeds
    {
        public EntireWorkflowSucceedsWithHeavyDiskSettings(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public async Task Execute()
        {
            await ExecuteInternalAsync();
        }

        protected override void ConfigureSettingsInternal(NuGetInsightsSettings x)
        {
            x.MaxTempMemoryStreamSize = 0;
            x.TempDirectories[0].MaxConcurrentWriters = 1;
        }

        protected override void ConfigureWorkerSettingsInternal(NuGetInsightsWorkerSettings x)
        {
            x.AppendResultBigModeRecordThreshold = 0;
        }
    }

    public abstract class EntireWorkflowSucceeds : EndToEndTest
    {
        public EntireWorkflowSucceeds(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
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

        protected virtual void ConfigureSettingsInternal(NuGetInsightsSettings x)
        {
        }

        protected virtual void ConfigureWorkerSettingsInternal(NuGetInsightsWorkerSettings x)
        {
        }

        protected async Task ExecuteInternalAsync()
        {
            // Arrange
            ConfigureSettings = x =>
            {
                x.DownloadsV1Urls = new List<string> { $"http://localhost/{TestInput}/DownloadsToCsv/downloads.v1.json" };
                x.OwnersV2Urls = new List<string> { $"http://localhost/{TestInput}/OwnersToCsv/owners.v2.json" };
                x.VerifiedPackagesV1Urls = new List<string> { $"http://localhost/{TestInput}/VerifiedPackagesToCsv/verifiedPackages.json" };
                x.ExcludedPackagesV1Urls = new List<string> { $"http://localhost/{TestInput}/ExcludedPackagesToCsv/excludedPackages.json" };
                x.PopularityTransfersV1Urls = new List<string> { $"http://localhost/{TestInput}/PopularityTransfersToCsv/popularity-transfers.v1.json" };
                ConfigureSettingsInternal(x);
            };
            ConfigureWorkerSettings = x =>
            {
                x.KustoConnectionString = "fake connection string";
                x.KustoDatabaseName = "fake database name";
                x.RecordCertificateStatus = false;
                ConfigureWorkerSettingsInternal(x);
            };

            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);
            var expectedContainers = GetExpectedContainers();

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
                    newReq.RequestUri = new Uri($"http://localhost/{TestInput}/DownloadsToCsv/{Step1}/downloads.v1.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                if (req.RequestUri.AbsolutePath.EndsWith("/owners.v2.json", StringComparison.Ordinal))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestInput}/OwnersToCsv/{Step1}/owners.v2.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                if (req.RequestUri.AbsolutePath.EndsWith("/verifiedPackages.json", StringComparison.Ordinal))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestInput}/VerifiedPackagesToCsv/{Step1}/verifiedPackages.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                if (req.RequestUri.AbsolutePath.EndsWith("/excludedPackages.json", StringComparison.Ordinal))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestInput}/ExcludedPackagesToCsv/{Step1}/excludedPackages.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                if (req.RequestUri.AbsolutePath.EndsWith("/popularity-transfers.v1.json", StringComparison.Ordinal))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestInput}/PopularityTransfersToCsv/{Step1}/popularity-transfers.v1.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };

            await WorkflowService.InitializeAsync();

            // Run the LoadBucketedPackage driver to so the timed reprocess can process something.
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            var initialLBP = await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, max1);

            foreach (var type in CatalogScanDriverMetadata.StartableDriverTypes)
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
                parallel: true);

            // Assert
            // Make sure all scans completed.
            var indexScans = await CatalogScanStorageService.GetIndexScansAsync();
            Assert.All(indexScans, x => Assert.Equal(CatalogIndexScanState.Complete, x.State));
            Assert.Equal(
                CatalogScanDriverMetadata.StartableDriverTypes.ToArray(),
                CatalogScanDriverMetadata.SortByTopologicalOrder(indexScans.Where(x => x.BucketRanges is null && x.ScanId != initialLBP.ScanId).Select(x => x.DriverType).Distinct(), x => x).ToArray());
            Assert.Equal(
                TimedReprocessService.GetReprocessBatches().SelectMany(b => b).Order().ToArray(),
                indexScans.Where(x => x.BucketRanges is not null).Select(x => x.DriverType).Distinct().Order().ToArray());

            // Make sure all of the containers are have ingestions
            foreach (var (containerName, recordType, defaultTableName) in expectedContainers)
            {
                MockKustoQueueIngestClient.Verify(x => x.IngestFromStorageAsync(
                    It.Is<string>(y => y.Contains(containerName)),
                    It.IsAny<KustoIngestionProperties>(),
                    It.IsAny<StorageSourceOptions>()));

                // Make sure there are metrics for each container.
                Assert.Contains(containerName, TelemetryClient.Metrics[new("CsvBlob.Count", "ContainerName")].MetricValues.Select(x => x.DimensionValues[0]));
                Assert.Contains(containerName, TelemetryClient.Metrics[new("CsvBlob.RecordCount", "ContainerName")].MetricValues.Select(x => x.DimensionValues[0]));
                Assert.Contains(containerName, TelemetryClient.Metrics[new("CsvBlob.CompressedSize", "ContainerName")].MetricValues.Select(x => x.DimensionValues[0]));
                Assert.Contains(containerName, TelemetryClient.Metrics[new("CsvBlob.UncompressedSize", "ContainerName")].MetricValues.Select(x => x.DimensionValues[0]));

                await AssertCsvAsync(recordType, containerName, nameof(EntireWorkflowSucceeds), Step1, 0, $"{defaultTableName}.csv");
            }

            // Make sure no workflow step is running.
            Assert.False(await WorkflowService.IsAnyWorkflowStepRunningAsync());
        }
    }
}

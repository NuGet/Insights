// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kusto.Ingest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoIngestionIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public class KustoIngestion_IngestsAllBlobs : KustoIngestionIntegrationTest
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ExecuteAsync(bool applyPartitioningPolicy)
            {
                ConfigureWorkerSettings = x =>
                {
                    x.KustoTableNameFormat = "A{0}Z";
                    x.KustoApplyPartitioningPolicy = applyPartitioningPolicy;
                    x.AppendResultStorageBucketCount = 2;
                };

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageManifest, max1);
                await SetCursorAsync(CatalogScanDriverType.CatalogLeafItemToCsv, min0);
                await SetCursorAsync(CatalogScanDriverType.PackageManifestToCsv, min0);
                var catalogLeafItemToCsvResult = await CatalogScanService.UpdateAsync(CatalogScanDriverType.CatalogLeafItemToCsv, max1);
                var packageManifestToCsvResult = await CatalogScanService.UpdateAsync(CatalogScanDriverType.PackageManifestToCsv, max1);
                await UpdateAsync(catalogLeafItemToCsvResult.Scan);
                await UpdateAsync(packageManifestToCsvResult.Scan);

                await KustoIngestionService.InitializeAsync();

                // Act
                var ingestion = await KustoIngestionService.StartAsync();
                ingestion = await UpdateAsync(ingestion);

                // Assert
                VerifyCommand(".drop table ACatalogLeafItemsZ_Temp ifexists", Times.Once());
                VerifyCommand(".drop table APackageManifestsZ_Temp ifexists", Times.Once());
                VerifyCommandStartsWith(".create table ACatalogLeafItemsZ_Temp (", Times.Once());
                VerifyCommandStartsWith(".create table APackageManifestsZ_Temp (", Times.Once());
                VerifyCommand(".alter-merge table ACatalogLeafItemsZ_Temp policy retention softdelete = 30d", Times.Once());
                VerifyCommand(".alter-merge table APackageManifestsZ_Temp policy retention softdelete = 30d", Times.Once());
                VerifyCommandStartsWith(".create table ACatalogLeafItemsZ_Temp ingestion csv mapping 'BlobStorageMapping'", Times.Once());
                VerifyCommandStartsWith(".create table APackageManifestsZ_Temp ingestion csv mapping 'BlobStorageMapping'", Times.Once());
                VerifyCommand(".drop tables (ACatalogLeafItemsZ_Old, APackageManifestsZ_Old) ifexists", Times.Exactly(2));
                VerifyCommand(".rename tables ACatalogLeafItemsZ_Old = ACatalogLeafItemsZ ifexists, ACatalogLeafItemsZ = ACatalogLeafItemsZ_Temp, APackageManifestsZ_Old = APackageManifestsZ ifexists, APackageManifestsZ = APackageManifestsZ_Temp", Times.Once());
                if (applyPartitioningPolicy)
                {
                    VerifyCommandStartsWith(".alter table ACatalogLeafItemsZ_Temp policy partitioning '{'", Times.Once());
                    VerifyCommandStartsWith(".alter table APackageManifestsZ_Temp policy partitioning '{'", Times.Once());
                }
                Assert.Equal(
                    applyPartitioningPolicy ? 13 : 11,
                    MockCslAdminProvider.Invocations.Count(x => x.Method.Name != nameof(IDisposable.Dispose)));

                MockKustoQueueIngestClient.Verify(x => x.IngestFromStorageAsync(
                    It.Is<string>(y => y.Contains($"/{Options.Value.CatalogLeafItemContainerName}/compact_0.csv.gz?")),
                    It.IsAny<KustoIngestionProperties>(),
                    It.IsAny<StorageSourceOptions>()), Times.Once);
                MockKustoQueueIngestClient.Verify(x => x.IngestFromStorageAsync(
                    It.Is<string>(y => y.Contains($"/{Options.Value.PackageManifestContainerName}/compact_0.csv.gz?")),
                    It.IsAny<KustoIngestionProperties>(),
                    It.IsAny<StorageSourceOptions>()), Times.Once);
                MockKustoQueueIngestClient.Verify(x => x.IngestFromStorageAsync(
                    It.Is<string>(y => y.Contains($"/{Options.Value.PackageManifestContainerName}/compact_1.csv.gz?")),
                    It.IsAny<KustoIngestionProperties>(),
                    It.IsAny<StorageSourceOptions>()), Times.Once);
                Assert.Equal(3, MockKustoQueueIngestClient.Invocations.Count(x => x.Method.Name != nameof(IDisposable.Dispose)));
                var urls = MockKustoQueueIngestClient
                    .Invocations
                    .Where(x => x.Method.Name == nameof(IKustoQueuedIngestClient.IngestFromStorageAsync))
                    .Select(x => (string)x.Arguments[0]);
                foreach (var url in urls)
                {
                    using var response = await Host.Services.GetRequiredService<HttpClient>().GetAsync(url);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }
            }

            public KustoIngestion_IngestsAllBlobs(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public class KustoIngestion_RetriesFailedBlob : KustoIngestionIntegrationTest
        {
            [Fact]
            public async Task ExecuteAsync()
            {
                ConfigureWorkerSettings = x =>
                {
                    x.KustoTableNameFormat = "A{0}Z";
                    x.AppendResultStorageBucketCount = 2;
                };

                // Arrange
                var attempt = 1;
                MockKustoQueueIngestClient
                    .Setup(x => x.IngestFromStorageAsync(
                        It.IsAny<string>(),
                        It.IsAny<KustoIngestionProperties>(),
                        It.IsAny<StorageSourceOptions>()))
                    .Returns<string, KustoIngestionProperties, StorageSourceOptions>(async (u, p, o) =>
                    {
                        if (u.Contains($"/{Options.Value.PackageManifestContainerName}/compact_0.csv.gz") && attempt <= 2)
                        {
                            attempt++;
                            return await MakeTableReportIngestionResultAsync(o, Status.Failed);
                        }
                        else
                        {
                            return await MakeTableReportIngestionResultAsync(o, Status.Succeeded);
                        }
                    });

                var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageManifest, max1);
                await SetCursorAsync(CatalogScanDriverType.CatalogLeafItemToCsv, min0);
                await SetCursorAsync(CatalogScanDriverType.PackageManifestToCsv, min0);
                var catalogLeafItemToCsvResult = await CatalogScanService.UpdateAsync(CatalogScanDriverType.CatalogLeafItemToCsv, max1);
                var packageManifestToCsvResult = await CatalogScanService.UpdateAsync(CatalogScanDriverType.PackageManifestToCsv, max1);
                await UpdateAsync(catalogLeafItemToCsvResult.Scan);
                await UpdateAsync(packageManifestToCsvResult.Scan);

                await KustoIngestionService.InitializeAsync();

                // Act
                var ingestion = await KustoIngestionService.StartAsync();
                ingestion = await UpdateAsync(ingestion);

                // Assert
                VerifyCommand(".drop table ACatalogLeafItemsZ_Temp ifexists", Times.Once());
                VerifyCommand(".drop table APackageManifestsZ_Temp ifexists", Times.Exactly(3));
                VerifyCommandStartsWith(".create table ACatalogLeafItemsZ_Temp (", Times.Once());
                VerifyCommandStartsWith(".create table APackageManifestsZ_Temp (", Times.Exactly(3));
                VerifyCommand(".alter-merge table ACatalogLeafItemsZ_Temp policy retention softdelete = 30d", Times.Once());
                VerifyCommand(".alter-merge table APackageManifestsZ_Temp policy retention softdelete = 30d", Times.Exactly(3));
                VerifyCommandStartsWith(".create table ACatalogLeafItemsZ_Temp ingestion csv mapping 'BlobStorageMapping'", Times.Once());
                VerifyCommandStartsWith(".create table APackageManifestsZ_Temp ingestion csv mapping 'BlobStorageMapping'", Times.Exactly(3));
                VerifyCommand(".drop tables (ACatalogLeafItemsZ_Old, APackageManifestsZ_Old) ifexists", Times.Exactly(2));
                VerifyCommand(".rename tables ACatalogLeafItemsZ_Old = ACatalogLeafItemsZ ifexists, ACatalogLeafItemsZ = ACatalogLeafItemsZ_Temp, APackageManifestsZ_Old = APackageManifestsZ ifexists, APackageManifestsZ = APackageManifestsZ_Temp", Times.Once());
                VerifyCommandStartsWith(".alter table ACatalogLeafItemsZ_Temp policy partitioning '{'", Times.Once());
                VerifyCommandStartsWith(".alter table APackageManifestsZ_Temp policy partitioning '{'", Times.Exactly(3));
                Assert.Equal(
                    23,
                    MockCslAdminProvider.Invocations.Count(x => x.Method.Name != nameof(IDisposable.Dispose)));

                MockKustoQueueIngestClient.Verify(x => x.IngestFromStorageAsync(
                    It.Is<string>(y => y.Contains($"/{Options.Value.CatalogLeafItemContainerName}/compact_0.csv.gz?")),
                    It.IsAny<KustoIngestionProperties>(),
                    It.IsAny<StorageSourceOptions>()), Times.Once);
                MockKustoQueueIngestClient.Verify(x => x.IngestFromStorageAsync(
                    It.Is<string>(y => y.Contains($"/{Options.Value.PackageManifestContainerName}/compact_0.csv.gz?")),
                    It.IsAny<KustoIngestionProperties>(),
                    It.IsAny<StorageSourceOptions>()), Times.Exactly(3));
                MockKustoQueueIngestClient.Verify(x => x.IngestFromStorageAsync(
                    It.Is<string>(y => y.Contains($"/{Options.Value.PackageManifestContainerName}/compact_1.csv.gz?")),
                    It.IsAny<KustoIngestionProperties>(),
                    It.IsAny<StorageSourceOptions>()), Times.Exactly(3));
                Assert.Equal(7, MockKustoQueueIngestClient.Invocations.Count(x => x.Method.Name != nameof(IDisposable.Dispose)));
                var urls = MockKustoQueueIngestClient
                    .Invocations
                    .Where(x => x.Method.Name == nameof(IKustoQueuedIngestClient.IngestFromStorageAsync))
                    .Select(x => (string)x.Arguments[0]);
                foreach (var url in urls)
                {
                    using var response = await Host.Services.GetRequiredService<HttpClient>().GetAsync(url);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }
            }

            public KustoIngestion_RetriesFailedBlob(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
                FailFastLogLevel = LogLevel.None;
                AssertLogLevel = LogLevel.None;
            }
        }

        public KustoIngestionIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        private void VerifyCommand(string command, Times times)
        {
            MockCslAdminProvider.Verify(x => x.ExecuteControlCommandAsync(Options.Value.KustoDatabaseName, command, null), times);
        }

        private void VerifyCommandStartsWith(string command, Times times)
        {
            MockCslAdminProvider.Verify(x => x.ExecuteControlCommandAsync(Options.Value.KustoDatabaseName, It.Is<string>(c => c.StartsWith(command)), null), times);
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
    }
}

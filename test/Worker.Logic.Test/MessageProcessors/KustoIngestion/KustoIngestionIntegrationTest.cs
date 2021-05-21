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
                VerifyCommand(".drop table ACatalogLeafItemsZ_Temp ifexists");
                VerifyCommand(".drop table APackageManifestsZ_Temp ifexists");
                VerifyCommandStartsWith(".create table ACatalogLeafItemsZ_Temp (");
                VerifyCommandStartsWith(".create table APackageManifestsZ_Temp (");
                VerifyCommand(".alter-merge table ACatalogLeafItemsZ_Temp policy retention softdelete = 30d");
                VerifyCommand(".alter-merge table APackageManifestsZ_Temp policy retention softdelete = 30d");
                VerifyCommandStartsWith(".create table ACatalogLeafItemsZ_Temp ingestion csv mapping 'BlobStorageMapping'");
                VerifyCommandStartsWith(".create table APackageManifestsZ_Temp ingestion csv mapping 'BlobStorageMapping'");
                VerifyCommand(".drop table ACatalogLeafItemsZ_Old ifexists");
                VerifyCommand(".drop table APackageManifestsZ_Old ifexists");
                VerifyCommand(".rename tables ACatalogLeafItemsZ_Old=ACatalogLeafItemsZ ifexists, ACatalogLeafItemsZ=ACatalogLeafItemsZ_Temp");
                VerifyCommand(".rename tables APackageManifestsZ_Old=APackageManifestsZ ifexists, APackageManifestsZ=APackageManifestsZ_Temp");
                VerifyCommand(".drop table ACatalogLeafItemsZ_Old ifexists");
                VerifyCommand(".drop table APackageManifestsZ_Old ifexists");
                if (applyPartitioningPolicy)
                {
                    VerifyCommandStartsWith(".alter table ACatalogLeafItemsZ_Temp policy partitioning '{'");
                    VerifyCommandStartsWith(".alter table APackageManifestsZ_Temp policy partitioning '{'");
                }
                Assert.Equal(
                    applyPartitioningPolicy ? 16 : 14,
                    MockCslAdminProvider.Invocations.Count(x => x.Method.Name != nameof(IDisposable.Dispose)));

                MockKustoQueueIngestClient.Verify(x => x.IngestFromStorageAsync(
                    It.Is<string>(y => y.Contains($"/{Options.Value.CatalogLeafItemContainerName}/compact_0.csv.gz?")),
                    It.IsAny<KustoIngestionProperties>(),
                    It.IsAny<StorageSourceOptions>()));
                MockKustoQueueIngestClient.Verify(x => x.IngestFromStorageAsync(
                    It.Is<string>(y => y.Contains($"/{Options.Value.PackageManifestContainerName}/compact_0.csv.gz?")),
                    It.IsAny<KustoIngestionProperties>(),
                    It.IsAny<StorageSourceOptions>()));
                MockKustoQueueIngestClient.Verify(x => x.IngestFromStorageAsync(
                    It.Is<string>(y => y.Contains($"/{Options.Value.PackageManifestContainerName}/compact_1.csv.gz?")),
                    It.IsAny<KustoIngestionProperties>(),
                    It.IsAny<StorageSourceOptions>()));
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

            private void VerifyCommand(string command)
            {
                MockCslAdminProvider.Verify(x => x.ExecuteControlCommandAsync(Options.Value.KustoDatabaseName, command, null));
            }

            private void VerifyCommandStartsWith(string command)
            {
                MockCslAdminProvider.Verify(x => x.ExecuteControlCommandAsync(Options.Value.KustoDatabaseName, It.Is<string>(c => c.StartsWith(command)), null));
            }

            public KustoIngestion_IngestsAllBlobs(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public KustoIngestionIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
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
    }
}

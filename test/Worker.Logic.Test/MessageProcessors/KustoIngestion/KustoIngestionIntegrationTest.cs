using System;
using System.Threading.Tasks;
using Kusto.Ingest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public class KustoIngestionIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public class KustoIngestion_IngestsAllBlobs : KustoIngestionIntegrationTest
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
                VerifyCommandStartsWith(".alter table ACatalogLeafItemsZ_Temp policy partitioning '{'");
                VerifyCommandStartsWith(".alter table APackageManifestsZ_Temp policy partitioning '{'");
                VerifyCommandStartsWith(".create table ACatalogLeafItemsZ_Temp ingestion csv mapping 'BlobStorageMapping'");
                VerifyCommandStartsWith(".create table APackageManifestsZ_Temp ingestion csv mapping 'BlobStorageMapping'");
                VerifyCommand(".drop table ACatalogLeafItemsZ_Old ifexists");
                VerifyCommand(".drop table APackageManifestsZ_Old ifexists");
                VerifyCommand(".rename tables ACatalogLeafItemsZ_Old=ACatalogLeafItemsZ ifexists, ACatalogLeafItemsZ=ACatalogLeafItemsZ_Temp");
                VerifyCommand(".rename tables APackageManifestsZ_Old=APackageManifestsZ ifexists, APackageManifestsZ=APackageManifestsZ_Temp");
                VerifyCommand(".drop table ACatalogLeafItemsZ_Old ifexists");
                VerifyCommand(".drop table APackageManifestsZ_Old ifexists");
                MockCslAdminProvider.Verify(x => x.Dispose(), Times.Exactly(12)); // From message processing scopes
                Assert.Equal(28, MockCslAdminProvider.Invocations.Count);

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
                MockKustoQueueIngestClient.Verify(x => x.Dispose(), Times.Exactly(3)); // From message processing scopes
                Assert.Equal(6, MockKustoQueueIngestClient.Invocations.Count);
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

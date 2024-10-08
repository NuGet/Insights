// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Kusto.Data.Common;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;

namespace NuGet.Insights.Worker.Workflow
{
    public class WorkflowIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public async Task Workflow_RetriesForFailedValidation()
        {

            // Arrange
            ConfigureWorkerSettings = x =>
            {
                x.KustoTableNameFormat = "A{0}Z";
                x.KustoConnectionString = "fake connection string";
                x.KustoDatabaseName = "fake database name";
                x.KustoValidationMaxAttempts = 1;
                x.DisabledDrivers = CatalogScanDriverMetadata.StartableDriverTypes
                    .Except([CatalogScanDriverType.LoadBucketedPackage, CatalogScanDriverType.LoadPackageReadme, CatalogScanDriverType.PackageReadmeToCsv, CatalogScanDriverType.CatalogDataToCsv])
                    .ToList();
            };

            var attempt = 0;
            MockCslQueryProvider
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ClientRequestProperties>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var mockReader = new Mock<IDataReader>();
                    mockReader.SetupSequence(x => x.Read()).Returns(true).Returns(false);
                    mockReader.Setup(x => x.GetValue(It.IsAny<int>())).Returns(new JValue((object)null));
                    if (attempt == 0)
                    {
                        mockReader
                            .SetupSequence(x => x.GetInt64(It.IsAny<int>()))
                            .Returns(0)
                            .Returns(1);
                    }
                    else
                    {
                        mockReader
                            .Setup(x => x.GetInt64(It.IsAny<int>()))
                            .Returns(0);
                    }
                    attempt++;
                    return mockReader.Object;
                });

            await WorkflowService.InitializeAsync();

            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z", CultureInfo.InvariantCulture);
            MockRemoteCursorClient
                .Setup(x => x.GetFlatContainerAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(max1);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageReadme, max1);
            await SetCursorsAsync([CatalogScanDriverType.LoadBucketedPackage, CatalogScanDriverType.PackageReadmeToCsv, CatalogScanDriverType.CatalogDataToCsv], min0);

            // Act
            var workflow = await WorkflowService.StartAsync();
            workflow = await UpdateAsync(workflow);

            // Assert
            Assert.Equal(WorkflowRunState.Complete, workflow.State);
            Assert.Equal(2, workflow.AttemptCount);
        }

        [Fact]
        public async Task Workflow_FailsIfKustoIngestionFailsTooMuch()
        {
            // Arrange
            ConfigureWorkerSettings = x =>
            {
                x.KustoTableNameFormat = "A{0}Z";
                x.KustoConnectionString = "fake connection string";
                x.KustoDatabaseName = "fake database name";
                x.KustoValidationMaxAttempts = 1;
                x.WorkflowMaxAttempts = 3;
                x.DisabledDrivers = CatalogScanDriverMetadata.StartableDriverTypes
                    .Except([CatalogScanDriverType.LoadBucketedPackage, CatalogScanDriverType.LoadPackageReadme, CatalogScanDriverType.PackageReadmeToCsv, CatalogScanDriverType.CatalogDataToCsv])
                    .ToList();
            };

            MockCslQueryProvider
                .Setup(x => x.ExecuteQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ClientRequestProperties>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var mockReader = new Mock<IDataReader>();
                    mockReader.SetupSequence(x => x.Read()).Returns(true).Returns(false);
                    mockReader.Setup(x => x.GetValue(It.IsAny<int>())).Returns(new JValue((object)null));
                    mockReader
                        .SetupSequence(x => x.GetInt64(It.IsAny<int>()))
                        .Returns(0)
                        .Returns(1);
                    return mockReader.Object;
                });

            await WorkflowService.InitializeAsync();

            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z", CultureInfo.InvariantCulture);
            MockRemoteCursorClient
                .Setup(x => x.GetFlatContainerAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(max1);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageReadme, max1);
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            await SetCursorAsync(CatalogScanDriverType.PackageReadmeToCsv, min0);
            await SetCursorAsync(CatalogScanDriverType.CatalogDataToCsv, min0);

            // Act & Assert
            var workflow = await WorkflowService.StartAsync();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateAsync(workflow));
            Assert.Equal("The workflow could not complete due to Kusto FailedValidation state after 3 attempts.", ex.Message);
            workflow = await WorkflowStorageService.GetRunAsync(workflow.RunId);
            Assert.Equal(WorkflowRunState.KustoIngestionWorking, workflow.State);
            Assert.Equal(3, workflow.AttemptCount);
        }

        [Fact]
        public async Task Workflow_FailsWithAbortedCatalogScan()
        {
            // Arrange
            ConfigureWorkerSettings = x =>
            {
                x.KustoTableNameFormat = "A{0}Z";
                x.KustoConnectionString = "fake connection string";
                x.KustoDatabaseName = "fake database name";
                x.KustoValidationMaxAttempts = 1;
                x.WorkflowMaxAttempts = 3;
                x.DisabledDrivers = CatalogScanDriverMetadata.StartableDriverTypes
                    .Except([CatalogScanDriverType.LoadBucketedPackage, CatalogScanDriverType.LoadPackageReadme, CatalogScanDriverType.PackageReadmeToCsv, CatalogScanDriverType.CatalogDataToCsv])
                    .ToList();
            };

            await WorkflowService.InitializeAsync();

            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z", CultureInfo.InvariantCulture);
            MockRemoteCursorClient
                .Setup(x => x.GetFlatContainerAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(max1);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageReadme, max1);
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            await SetCursorAsync(CatalogScanDriverType.PackageReadmeToCsv, min0);
            await SetCursorAsync(CatalogScanDriverType.CatalogDataToCsv, min0);

            var catalogScanResult = await CatalogScanService.UpdateAsync(CatalogScanDriverType.CatalogDataToCsv);
            await CatalogScanService.AbortAsync(CatalogScanDriverType.CatalogDataToCsv);

            // Act & Assert
            var workflow = await WorkflowService.StartAsync();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateAsync(workflow));
            Assert.Equal("The CatalogScanUpdate timer could not be started.", ex.Message);
            workflow = await WorkflowStorageService.GetRunAsync(workflow.RunId);
            Assert.Equal(WorkflowRunState.Created, workflow.State);
            Assert.Equal(1, workflow.AttemptCount);
            var scans = await CatalogScanStorageService.GetIndexScansAsync();
            var onlyScan = Assert.Single(scans);
            Assert.Equal(CatalogIndexScanState.Aborted, onlyScan.State);
            Assert.Equal(catalogScanResult.Scan.ScanId, onlyScan.ScanId);
        }

        [Fact]
        public async Task Workflow_SucceedsIfAllCatalogScansAreCaughtUp()
        {
            // Arrange
            ConfigureWorkerSettings = x =>
            {
                x.KustoTableNameFormat = "A{0}Z";
                x.KustoConnectionString = "fake connection string";
                x.KustoDatabaseName = "fake database name";
                x.KustoValidationMaxAttempts = 1;
                x.WorkflowMaxAttempts = 3;
                x.DisabledDrivers = CatalogScanDriverMetadata.StartableDriverTypes
                    .Except([CatalogScanDriverType.LoadBucketedPackage, CatalogScanDriverType.LoadPackageReadme, CatalogScanDriverType.PackageReadmeToCsv, CatalogScanDriverType.CatalogDataToCsv])
                    .ToList();
            };

            await WorkflowService.InitializeAsync();

            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z", CultureInfo.InvariantCulture);
            MockRemoteCursorClient
                .Setup(x => x.GetFlatContainerAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(max1);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, max1);
            await SetCursorAsync(CatalogScanDriverType.LoadPackageReadme, max1);
            await SetCursorAsync(CatalogScanDriverType.PackageReadmeToCsv, max1);
            await SetCursorAsync(CatalogScanDriverType.CatalogDataToCsv, max1);

            // Act
            var workflow = await WorkflowService.StartAsync();
            workflow = await UpdateAsync(workflow);

            // Assert
            Assert.Equal(WorkflowRunState.Complete, workflow.State);
            Assert.Equal(1, workflow.AttemptCount);
            Assert.All(await CatalogScanStorageService.GetIndexScansAsync(), x => Assert.NotNull(x.BucketRanges));
        }

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddSingleton(MockRemoteCursorClient.Object);
                serviceCollection.AddMockKusto(this);
            });
        }

        public Mock<IRemoteCursorClient> MockRemoteCursorClient { get; }

        public WorkflowIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            FailFastLogLevel = LogLevel.None;
            AssertLogLevel = LogLevel.None;

            MockRemoteCursorClient = new Mock<IRemoteCursorClient>();
        }
    }
}

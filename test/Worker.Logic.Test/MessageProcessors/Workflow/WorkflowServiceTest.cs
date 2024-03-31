// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.AuxiliaryFileUpdater;

namespace NuGet.Insights.Worker.Workflow
{
    public class WorkflowServiceTest : BaseWorkerLogicIntegrationTest
    {
        public WorkflowServiceTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public async Task StartsRunWhenNothingIsRunning()
        {
            await WorkflowService.InitializeAsync();

            var run = await WorkflowService.StartAsync();

            Assert.NotNull(run);
            var runs = await WorkflowStorageService.GetRunsAsync();
            var actualRun = Assert.Single(runs);
            Assert.Equal(run.RunId, actualRun.RunId);
        }

        [Fact]
        public async Task DoesNotStartWhenAlreadyStarted()
        {
            await WorkflowService.InitializeAsync();
            var initialRun = await WorkflowService.StartAsync();

            var run = await WorkflowService.StartAsync();

            Assert.NotNull(initialRun);
            Assert.Null(run);
            Assert.Single(await WorkflowStorageService.GetRunsAsync());
        }

        [Fact]
        public async Task DoesNotStartWhenCatalogScanIsRunning()
        {
            await WorkflowService.InitializeAsync();
            await CatalogScanService.InitializeAsync();
            var initialRun = await CatalogScanService.UpdateAsync(CatalogScanDriverType.BuildVersionSet);

            var run = await WorkflowService.StartAsync();

            Assert.NotNull(initialRun);
            Assert.Null(run);
            Assert.Empty(await WorkflowStorageService.GetRunsAsync());
        }

        [Fact]
        public async Task DoesNotStartWhenOwnersToCsvIsRunning()
        {
            await WorkflowService.InitializeAsync();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            var run = await WorkflowService.StartAsync();

            Assert.Null(run);
            Assert.Empty(await WorkflowStorageService.GetRunsAsync());
        }

        [Fact]
        public async Task DoesNotStartWhenDownloadsToCsvIsRunning()
        {
            await WorkflowService.InitializeAsync();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageDownloads>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            var run = await WorkflowService.StartAsync();

            Assert.Null(run);
            Assert.Empty(await WorkflowStorageService.GetRunsAsync());
        }

        [Fact]
        public async Task DoesNotStartWhenVerifiedPackagesToCsvIsRunning()
        {
            await WorkflowService.InitializeAsync();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            var run = await WorkflowService.StartAsync();

            Assert.Null(run);
            Assert.Empty(await WorkflowStorageService.GetRunsAsync());
        }

        [Fact]
        public async Task DoesNotStartWhenExcludedPackagesToCsvIsRunning()
        {
            await WorkflowService.InitializeAsync();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<ExcludedPackage>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            var run = await WorkflowService.StartAsync();

            Assert.Null(run);
            Assert.Empty(await WorkflowStorageService.GetRunsAsync());
        }

        [Fact]
        public async Task DoesNotStartWhenPopularityTransfersToCsvIsRunning()
        {
            await WorkflowService.InitializeAsync();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PopularityTransfer>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            var run = await WorkflowService.StartAsync();

            Assert.Null(run);
            Assert.Empty(await WorkflowStorageService.GetRunsAsync());
        }

        [Fact]
        public async Task DoesNotStartWhenKustoIngestionIsRunning()
        {
            await WorkflowService.InitializeAsync();
            await KustoIngestionService.InitializeAsync();
            var initialRun = await KustoIngestionService.StartAsync();

            var run = await WorkflowService.StartAsync();

            Assert.NotNull(initialRun);
            Assert.Null(run);
            Assert.Empty(await WorkflowStorageService.GetRunsAsync());
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.Workflow
{
    public class WorkflowServiceTest : BaseWorkerLogicIntegrationTest
    {
        public WorkflowServiceTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Theory]
        [InlineData(null)]
        [InlineData("2015-02-01T06:22:45.8488496Z")]
        public async Task StartsRunWhenNothingIsRunning(string maxCommitTimestamp)
        {
            var parsedMaxCommitTimestamp = maxCommitTimestamp == null ? (DateTimeOffset?)null : DateTimeOffset.Parse(maxCommitTimestamp);
            await WorkflowService.InitializeAsync();

            var run = await WorkflowService.StartAsync(parsedMaxCommitTimestamp);

            Assert.NotNull(run);
            Assert.Equal(parsedMaxCommitTimestamp, run.MaxCommitTimestamp);
            var runs = await WorkflowStorageService.GetRunsAsync();
            var actualRun = Assert.Single(runs);
            Assert.Equal(run.GetRunId(), actualRun.GetRunId());
        }

        [Fact]
        public async Task DoesNotStartWhenAlreadyStarted()
        {
            await WorkflowService.InitializeAsync();
            await WorkflowService.StartAsync(maxCommitTimestamp: null);

            var run = await WorkflowService.StartAsync(maxCommitTimestamp: null);

            Assert.Null(run);
            Assert.Single(await WorkflowStorageService.GetRunsAsync());
        }

        [Fact]
        public async Task DoesNotStartWhenCatalogScanIsRunning()
        {
            await WorkflowService.InitializeAsync();
            await CatalogScanService.InitializeAsync();
            await CatalogScanService.UpdateAsync(CatalogScanDriverType.BuildVersionSet);

            var run = await WorkflowService.StartAsync(maxCommitTimestamp: null);

            Assert.Null(run);
            Assert.Empty(await WorkflowStorageService.GetRunsAsync());
        }

        [Fact]
        public async Task DoesNotStartWhenOwnersToCsvIsRunning()
        {
            await WorkflowService.InitializeAsync();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageOwnerSet>>();
            await service.InitializeAsync();
            await service.StartAsync();

            var run = await WorkflowService.StartAsync(maxCommitTimestamp: null);

            Assert.Null(run);
            Assert.Empty(await WorkflowStorageService.GetRunsAsync());
        }

        [Fact]
        public async Task DoesNotStartWhenDownloadsToCsvIsRunning()
        {
            await WorkflowService.InitializeAsync();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageDownloadSet>>();
            await service.InitializeAsync();
            await service.StartAsync();

            var run = await WorkflowService.StartAsync(maxCommitTimestamp: null);

            Assert.Null(run);
            Assert.Empty(await WorkflowStorageService.GetRunsAsync());
        }

        [Fact]
        public async Task DoesNotStartWhenKustoIngestionIsRunning()
        {
            await WorkflowService.InitializeAsync();
            await KustoIngestionService.InitializeAsync();
            await KustoIngestionService.StartAsync();

            var run = await WorkflowService.StartAsync(maxCommitTimestamp: null);

            Assert.Null(run);
            Assert.Empty(await WorkflowStorageService.GetRunsAsync());
        }
    }
}

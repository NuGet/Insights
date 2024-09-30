// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class CatalogScanFaultInjectionTest : BaseWorkerLogicIntegrationTest
    {
        [NoInMemoryStorageFact]
        public async Task CanRecoverPageScanFanOutProblemWithRequeue()
        {
            // Arrange
            FailFastLogLevel = LogLevel.Error;
            AssertLogLevel = LogLevel.Error;
            ConfigureWorkerSettings = x => x.FanOutRequeueTime = TimeSpan.FromSeconds(10);

            var min0 = DateTimeOffset.Parse("2024-01-01T00:03:03.6703369Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2024-01-01T00:05:46.7438130Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.CatalogDataToCsv, min0);
            var result = await CatalogScanService.UpdateAsync(CatalogScanDriverType.CatalogDataToCsv, max1);
            Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
            var scan = result.Scan;

            var startedFirstPageExpand = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var workingPages = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var finishedFirstPageExpand = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                HttpResponseMessage response = null;

                if (r.Method == HttpMethod.Post
                    && r.RequestUri.AbsolutePath.EndsWith($"/{Options.Value.CatalogPageScanTableNamePrefix}{scan.StorageSuffix}", StringComparison.Ordinal))
                {
                    var latestScan = await CatalogScanStorageService.GetIndexScanAsync(scan.DriverType, scan.ScanId);
                    if (latestScan.State == CatalogIndexScanState.Expanding)
                    {
                        if (startedFirstPageExpand.TrySetResult())
                        {
                            // #1 - make the first attempt to insert of page scans wait
                            await workingPages.Task;
                            response = await b(r, CancellationToken.None);
                            finishedFirstPageExpand.TrySetResult();
                        }
                        else
                        {
                            // #2 - make the second attempt to insert page scans wait on the first starting, but not completing
                            await startedFirstPageExpand.Task;
                            response = await b(r, CancellationToken.None);
                        }
                    }
                }

                if (r.Method == HttpMethod.Delete
                    && r.RequestUri.AbsolutePath.Contains($"/{Options.Value.CatalogPageScanTableNamePrefix}{scan.StorageSuffix}(PartitionKey='", StringComparison.Ordinal))
                {
                    response = await b(r, CancellationToken.None);

                    var latestScan = await CatalogScanStorageService.GetIndexScanAsync(scan.DriverType, scan.ScanId);
                    if (latestScan.State == CatalogIndexScanState.Working
                        && await CatalogScanStorageService.GetPageScanCountLowerBoundAsync(scan.StorageSuffix, scan.StorageSuffix) == 0)
                    {
                        // #3 - if the page scans complete and the scan is in working state, allow the first attempt to insert page scans to complete
                        workingPages.TrySetResult();
                        await finishedFirstPageExpand.Task;
                    }
                }

                return response;
            };

            // Act
            await UpdateAsync(
                scan,
                workerCount: 2,
                visibilityTimeout: TimeSpan.FromSeconds(1),
                retryFailedMessages: true);

            // Assert
            Assert.True(TelemetryClient.Metrics.TryGetValue(new("FanOutRecoveryService.UnstartedWorkCount", "Type"), out var metric));
            Assert.Contains(metric.MetricValues, x => x.DimensionValues[0] == "CatalogPageScan" && x.MetricValue == 1);
        }

        [NoInMemoryStorageFact]
        public async Task CanRecoverLeafScanFanOutProblemWithRequeue()
        {
            // Arrange
            FailFastLogLevel = LogLevel.Error;
            AssertLogLevel = LogLevel.Error;
            ConfigureWorkerSettings = x => x.FanOutRequeueTime = TimeSpan.FromSeconds(10);

            var min0 = DateTimeOffset.Parse("2024-01-01T00:03:03.6703369Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2024-01-01T00:05:46.7438130Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.CatalogDataToCsv, min0);
            var result = await CatalogScanService.UpdateAsync(CatalogScanDriverType.CatalogDataToCsv, max1);
            Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
            var scan = result.Scan;
            var pageId = "P0000019878";

            var startedFirstLeafExpand = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var workingLeaves = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var finishedFirstLeafExpand = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                HttpResponseMessage response = null;

                if (r.Method == HttpMethod.Post
                    && r.RequestUri.AbsolutePath.EndsWith($"/{Options.Value.CatalogLeafScanTableNamePrefix}{scan.StorageSuffix}", StringComparison.Ordinal))
                {
                    var pageScan = await CatalogScanStorageService.GetPageScanAsync(scan.StorageSuffix, scan.ScanId, pageId);
                    if (pageScan.State == CatalogPageScanState.Expanding)
                    {
                        if (startedFirstLeafExpand.TrySetResult())
                        {
                            // #1 - make the first attempt to insert of leaf scans wait
                            await workingLeaves.Task;
                            response = await b(r, CancellationToken.None);
                            finishedFirstLeafExpand.TrySetResult();
                        }
                        else
                        {
                            // #2 - make the second attempt to insert leaf scans wait on the first starting, but not completing
                            await startedFirstLeafExpand.Task;
                            response = await b(r, CancellationToken.None);
                        }
                    }
                }

                if (r.Method == HttpMethod.Delete
                    && r.RequestUri.AbsolutePath.Contains($"/{Options.Value.CatalogLeafScanTableNamePrefix}{scan.StorageSuffix}(PartitionKey='", StringComparison.Ordinal))
                {
                    response = await b(r, CancellationToken.None);

                    if (await CatalogScanStorageService.GetLeafScanCountLowerBoundAsync(scan.StorageSuffix, scan.StorageSuffix) == 0)
                    {
                        // #3 - if the leaf scans complete, allow the first attempt to insert leaf scans to complete
                        workingLeaves.TrySetResult();
                        await finishedFirstLeafExpand.Task;
                    }
                }

                return response;
            };

            // Act
            await UpdateAsync(
                scan,
                workerCount: 2,
                visibilityTimeout: TimeSpan.FromSeconds(1),
                retryFailedMessages: true);

            // Assert
            Assert.True(TelemetryClient.Metrics.TryGetValue(new("FanOutRecoveryService.UnstartedWorkCount", "Type"), out var metric));
            Assert.Contains(metric.MetricValues, x => x.DimensionValues[0] == "CatalogLeafScan" && x.MetricValue == 1);
        }

        public CatalogScanFaultInjectionTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}

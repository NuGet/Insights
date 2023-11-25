// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.RegularExpressions;
using Azure;

namespace NuGet.Insights.Worker
{
    public class TestFrameworkTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public async Task SkipsMessageThatIsAlreadyBeingProcessed()
        {
            // Arrange
            await CatalogScanService.InitializeAsync();
            var result = await CatalogScanService.UpdateAsync(CatalogScanDriverType.CatalogDataToCsv, CatalogClient.NuGetOrgFirstCommit);
            Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
            var scan = result.Scan;

            var firstGetTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstUpdateTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                if (r.RequestUri.AbsolutePath.EndsWith($"(PartitionKey='{scan.PartitionKey}',RowKey='{scan.RowKey}')", StringComparison.Ordinal))
                {
                    if (r.Method == HttpMethod.Get && firstGetTask.TrySetResult())
                    {
                        await firstUpdateTask.Task;
                    }

                    if (r.Method != HttpMethod.Get)
                    {
                        await firstGetTask.Task;
                        firstUpdateTask.TrySetResult();
                    }
                }

                return null;
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<RequestFailedException>(() => ProcessQueueAsync(
                () => Task.FromResult(firstGetTask.Task.IsCompleted && firstUpdateTask.Task.IsCompleted),
                parallel: true,
                visibilityTimeout: TimeSpan.FromSeconds(1)));

            Assert.Equal(HttpStatusCode.PreconditionFailed, (HttpStatusCode)ex.Status);
            Assert.Contains(
                LogMessages,
                x => Regex.IsMatch(x, "Skipping message .+? because it's already being processed") && x.Contains(scan.ScanId, StringComparison.Ordinal));
        }

        public TestFrameworkTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.RegularExpressions;

namespace NuGet.Insights.Worker
{
    public class TestFrameworkTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public async Task SkipsMessageThatIsAlreadyBeingProcessed()
        {
            // Arrange
            LogMessages.Limit = int.MaxValue;
            await CatalogScanService.InitializeAsync();
            var result = await CatalogScanService.UpdateAsync(CatalogScanDriverType.CatalogDataToCsv, CatalogClient.NuGetOrgFirstCommit);
            Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
            var scan = result.Scan;

            var getTask1 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var getTask2 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                if (r.RequestUri.AbsoluteUri == "https://api.nuget.org/v3/catalog0/index.json")
                {
                    if (getTask1.TrySetResult())
                    {
                        await getTask2.Task;
                    }
                    else
                    {
                        await getTask1.Task;
                        getTask2.TrySetResult();
                    }
                }

                return null;
            };

            // Act
            try
            {
                await ProcessQueueAsync(
                    () => Task.FromResult(getTask1.Task.IsCompleted && getTask2.Task.IsCompleted),
                    parallel: true,
                    visibilityTimeout: TimeSpan.FromSeconds(1));
            }
            catch
            {
                // ignore
            }

            // Assert
            Assert.Contains(
                LogMessages,
                x => Regex.IsMatch(x, "Skipping message .+? because it's already being processed") && x.Contains(scan.ScanId, StringComparison.Ordinal));
        }

        public TestFrameworkTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}

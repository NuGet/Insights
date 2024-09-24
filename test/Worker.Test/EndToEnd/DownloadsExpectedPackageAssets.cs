// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class DownloadsExpectedPackageAssets : EndToEndTest
    {
        public DownloadsExpectedPackageAssets(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public async Task Execute()
        {
            // Arrange
            HttpMessageHandlerFactory.Requests.Limit = int.MaxValue;
            HttpMessageHandlerFactory.RequestAndResponses.Limit = int.MaxValue;
            ConfigureSettings = x =>
            {
                x.LegacyReadmeUrlPattern = "https://api.nuget.org/legacy-readmes/{0}/{1}/README.md"; // fake
                x.MaxTempMemoryStreamSize = 0;
                x.TempDirectories[0].MaxConcurrentWriters = 1;
            };

            await CatalogScanService.InitializeAsync();

            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);

            foreach (var type in CatalogScanDriverMetadata.StartableDriverTypes)
            {
                await SetCursorAsync(type, min0);
            }

            // Act
            var nuspecRequestCount0 = GetNuspecRequestCount();
            var readmeRequestCount0 = GetReadmeRequestCount();
            var snupkgRequestCount0 = GetSnupkgRequestCount();
            var nupkgRequestCount0 = GetNupkgRequestCount();

            // Load the .nuspec files (manifests)
            await UpdateInBatchesAsync(CatalogScanDriverMetadata.GetDriversThatDownloadPackageAssets(DownloadedPackageAssets.Nuspec), max1);
            var nuspecRequestCount1 = GetNuspecRequestCount();

            // Load the readme files
            await UpdateInBatchesAsync(CatalogScanDriverMetadata.GetDriversThatDownloadPackageAssets(DownloadedPackageAssets.Readme), max1);
            var readmeRequestCount1 = GetReadmeRequestCount();

            // Run drivers that download both .snupkg and .nupkg
            var snupkgAndNupkg = CatalogScanDriverMetadata.GetDriversThatDownloadPackageAssets(DownloadedPackageAssets.Nupkg | DownloadedPackageAssets.Snupkg);
            if (snupkgAndNupkg.Count > 0)
            {
                await UpdateInBatchesAsync(snupkgAndNupkg, max1);
            }
            var snupkgRequestCount1 = GetSnupkgRequestCount();
            var nupkgRequestCount1 = GetNupkgRequestCount();

            // Run drivers that download .snupkg but not .nupkg
            await UpdateInBatchesAsync(CatalogScanDriverMetadata.GetDriversThatDownloadPackageAssets(DownloadedPackageAssets.Snupkg), max1);
            var snupkgRequestCount2 = GetSnupkgRequestCount();

            // Run drivers that download .nupkg but not .snupkg
            await UpdateInBatchesAsync(CatalogScanDriverMetadata.GetDriversThatDownloadPackageAssets(DownloadedPackageAssets.Nupkg), max1);
            var nupkgRequestCount2 = GetNupkgRequestCount();

            // Run the remaining drivers
            await UpdateInBatchesAsync(CatalogScanDriverMetadata.StartableDriverTypes, max1);

            var finalNuspecRequestCount = GetNuspecRequestCount();
            var finalReadmeRequestCount = GetReadmeRequestCount();
            var finalSnupkgRequestCount = GetSnupkgRequestCount();
            var finalNupkgRequestCount = GetNupkgRequestCount();

            // Assert
            var rawMessageEnqueuer = Host.Services.GetRequiredService<IRawMessageEnqueuer>();
            foreach (var queue in Enum.GetValues(typeof(QueueType)).Cast<QueueType>())
            {
                Assert.Equal(0, await rawMessageEnqueuer.GetApproximateMessageCountAsync(queue));
                Assert.Equal(0, await rawMessageEnqueuer.GetAvailableMessageCountLowerBoundAsync(queue, 32));
                Assert.Equal(0, await rawMessageEnqueuer.GetPoisonApproximateMessageCountAsync(queue));
                Assert.Equal(0, await rawMessageEnqueuer.GetPoisonAvailableMessageCountLowerBoundAsync(queue, 32));
            }

            Assert.Equal(0, nuspecRequestCount0);
            Assert.Equal(0, readmeRequestCount0);
            Assert.Equal(0, snupkgRequestCount0);
            Assert.Equal(0, nupkgRequestCount0);

            Assert.NotEqual(0, nuspecRequestCount1);
            Assert.NotEqual(0, readmeRequestCount1);

            if (snupkgAndNupkg.Count > 0)
            {
                Assert.NotEqual(0, snupkgRequestCount1);
                Assert.NotEqual(0, nupkgRequestCount1);
            }

            Assert.Equal(nuspecRequestCount1, finalNuspecRequestCount);
            Assert.Equal(readmeRequestCount1, finalReadmeRequestCount);
            Assert.Equal(snupkgRequestCount2, finalSnupkgRequestCount);
            Assert.Equal(nupkgRequestCount2, finalNupkgRequestCount);

            var userAgents = HttpMessageHandlerFactory.Responses.Select(r => r.RequestMessage.Headers.UserAgent.ToString()).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            foreach (var userAgent in userAgents)
            {
                Logger.LogInformation("Found User-Agent: {UserAgent}", userAgent);
            }

            Assert.Equal(Options.Value.UseMemoryStorage ? 3 : 4, userAgents.Count); // NuGet Insights, and Blob + Queue + Table Azure SDK.
            Assert.StartsWith("Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)", userAgents[0], StringComparison.Ordinal);
            var i = 0;
            Assert.Matches(@"(NuGet Test Client)/?(\d+)?\.?(\d+)?\.?(\d+)?", userAgents[i++]);
            Assert.StartsWith("azsdk-net-Data.Tables/", userAgents[i++], StringComparison.Ordinal);
            if (!Options.Value.UseMemoryStorage)
            {
                Assert.StartsWith("azsdk-net-Storage.Blobs/", userAgents[i++], StringComparison.Ordinal);
            }
            Assert.StartsWith("azsdk-net-Storage.Queues/", userAgents[i++], StringComparison.Ordinal);
        }
    }
}

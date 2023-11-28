// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class DownloadsExpectedNupkgsAndNuspecs : EndToEndTest
    {
        public DownloadsExpectedNupkgsAndNuspecs(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public async Task Execute()
        {
            // Arrange
            ConfigureSettings = x =>
            {
                x.LegacyReadmeUrlPattern = "https://api.nuget.org/legacy-readmes/{0}/{1}/README.md"; // fake
                x.MaxTempMemoryStreamSize = 0;
                x.TempDirectories[0].MaxConcurrentWriters = 1;
            };
            ConfigureWorkerSettings = x =>
            {
                x.AppendResultStorageBucketCount = 1;
            };

            await CatalogScanService.InitializeAsync();

            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);

            foreach (var type in CatalogScanDriverMetadata.StartableDriverTypes)
            {
                await SetCursorAsync(type, min0);
            }

            // Act

            // Load the manifests
            var loadPackageManifest = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageManifest, max1);
            await UpdateAsync(loadPackageManifest);

            var startingNuspecRequestCount = GetNuspecRequestCount();

            // Load the readmes
            var loadPackageReadme = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageReadme, max1);
            await UpdateAsync(loadPackageReadme);

            var startingReadmeRequestCount = GetReadmeRequestCount();

            // Load latest package leaves
            var loadLatestPackageLeaf = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadLatestPackageLeaf, max1);
            await UpdateAsync(loadLatestPackageLeaf);

            Assert.Equal(0, GetNupkgRequestCount());

            // Load the symbol packages
            var loadSymbolPackageArchive = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadSymbolPackageArchive, max1);
            await UpdateAsync(loadSymbolPackageArchive);

            var startingSnupkgRequestCount = GetSnupkgRequestCount();

            // Load the packages, process package assemblies, and run NuGet Package Explorer.
            var loadPackageArchive = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await UpdateAsync(loadPackageArchive);
            var packageAssemblyToCsv = await CatalogScanService.UpdateAsync(CatalogScanDriverType.PackageAssemblyToCsv, max1);
            var packageContentToCsv = await CatalogScanService.UpdateAsync(CatalogScanDriverType.PackageContentToCsv, max1);
#if ENABLE_NPE
            var nuGetPackageExplorerToCsv = await CatalogScanService.UpdateAsync(CatalogScanDriverType.NuGetPackageExplorerToCsv, max1);
#endif
            await UpdateAsync(packageAssemblyToCsv);
            await UpdateAsync(packageContentToCsv);
#if ENABLE_NPE
            await UpdateAsync(nuGetPackageExplorerToCsv);
#endif

            var startingNupkgRequestCount = GetNupkgRequestCount();
            var intermediateSnupkgRequestCount = GetSnupkgRequestCount();

            // Load the versions
            var loadPackageVersion = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageVersion, max1);
            await UpdateAsync(loadPackageVersion);

            // Start all of the scans
            var startedScans = new List<CatalogIndexScan>();
            foreach (var type in CatalogScanDriverMetadata.StartableDriverTypes)
            {
                var startedScan = await CatalogScanService.UpdateAsync(type, max1);
                if (startedScan.Type == CatalogScanServiceResultType.FullyCaughtUpWithMax)
                {
                    continue;
                }
                Assert.Equal(CatalogScanServiceResultType.NewStarted, startedScan.Type);
                startedScans.Add(startedScan.Scan);
            }

            // Wait for all of the scans to complete.
            foreach (var scan in startedScans)
            {
                await UpdateAsync(scan);
            }

            var finalNupkgRequestCount = GetNupkgRequestCount();
            var finalNuspecRequestCount = GetNuspecRequestCount();
            var finalReadmeRequestCount = GetReadmeRequestCount();
            var finalSnupkgRequestCount = GetSnupkgRequestCount();

            // Assert
            var rawMessageEnqueuer = Host.Services.GetRequiredService<IRawMessageEnqueuer>();
            foreach (var queue in Enum.GetValues(typeof(QueueType)).Cast<QueueType>())
            {
                Assert.Equal(0, await rawMessageEnqueuer.GetApproximateMessageCountAsync(queue));
                Assert.Equal(0, await rawMessageEnqueuer.GetAvailableMessageCountLowerBoundAsync(queue, 32));
                Assert.Equal(0, await rawMessageEnqueuer.GetPoisonApproximateMessageCountAsync(queue));
                Assert.Equal(0, await rawMessageEnqueuer.GetPoisonAvailableMessageCountLowerBoundAsync(queue, 32));
            }

            Assert.NotEqual(0, startingNupkgRequestCount);
            Assert.NotEqual(0, startingNuspecRequestCount);
            Assert.NotEqual(0, startingReadmeRequestCount);
            Assert.NotEqual(0, startingSnupkgRequestCount);
#if ENABLE_NPE
            Assert.NotEqual(startingSnupkgRequestCount, intermediateSnupkgRequestCount);
#else
                Assert.Equal(startingSnupkgRequestCount, intermediateSnupkgRequestCount);
#endif
            Assert.Equal(startingNupkgRequestCount, finalNupkgRequestCount);
            Assert.Equal(startingNuspecRequestCount, finalNuspecRequestCount);
            Assert.Equal(startingReadmeRequestCount, finalReadmeRequestCount);
            Assert.Equal(intermediateSnupkgRequestCount, finalSnupkgRequestCount);

            var userAgents = HttpMessageHandlerFactory.Responses.Select(r => r.RequestMessage.Headers.UserAgent.ToString()).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            foreach (var userAgent in userAgents)
            {
                Logger.LogInformation("Found User-Agent: {UserAgent}", userAgent);
            }

            Assert.Equal(4, userAgents.Count); // NuGet Insights, and Blob + Queue + Table Azure SDK.
            Assert.StartsWith("Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)", userAgents[0], StringComparison.Ordinal);
            Assert.Matches(@"(NuGet Test Client)/?(\d+)?\.?(\d+)?\.?(\d+)?", userAgents[0]);
            Assert.StartsWith("azsdk-net-Data.Tables/", userAgents[1], StringComparison.Ordinal);
            Assert.StartsWith("azsdk-net-Storage.Blobs/", userAgents[2], StringComparison.Ordinal);
            Assert.StartsWith("azsdk-net-Storage.Queues/", userAgents[3], StringComparison.Ordinal);
        }

        private int GetNuspecRequestCount()
        {
            return HttpMessageHandlerFactory.Responses.Count(x => x.RequestMessage.RequestUri.AbsoluteUri.EndsWith(".nuspec", StringComparison.Ordinal));
        }

        private int GetNupkgRequestCount()
        {
            return HttpMessageHandlerFactory.Responses.Count(x => x.RequestMessage.RequestUri.AbsoluteUri.EndsWith(".nupkg", StringComparison.Ordinal));
        }

        private int GetReadmeRequestCount()
        {
            return HttpMessageHandlerFactory.Responses.Count(x => x.RequestMessage.RequestUri.AbsoluteUri.EndsWith(".md", StringComparison.Ordinal));
        }

        private int GetSnupkgRequestCount()
        {
            return HttpMessageHandlerFactory.Responses.Count(x => x.RequestMessage.RequestUri.AbsoluteUri.EndsWith(".snupkg", StringComparison.Ordinal));
        }
    }
}

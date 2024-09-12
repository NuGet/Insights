// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class CatalogScanDriverMetadataIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public async Task DriverDownloadsExpectedAssets_PackageWithAllExtraAssets(string typeName)
        {
            // Arrange
            ConfigureWorkerSettings = x => x.PackageContentFileExtensions = [".nuspec"];
            var type = CatalogScanDriverType.Parse(typeName);
            var max1 = DateTimeOffset.Parse("2024-01-30T21:43:02.4985586Z", CultureInfo.InvariantCulture);
            var min0 = max1.Subtract(TimeSpan.FromTicks(1));

            await CatalogScanService.InitializeAsync();
            await SetCursorsAsync(CatalogScanDriverMetadata.StartableDriverTypes, min0);
            await UpdateInBatchesAsync(CatalogScanDriverMetadata.GetTransitiveClosure(type).Except([type]), max1);
            HttpMessageHandlerFactory.Clear();
            Output.WriteHorizontalRule();
            Output.WriteLine($"Starting driver under test: {type}");
            Output.WriteHorizontalRule();

            // Act
            var scan = await UpdateAsync(type, max1);

            // Assert
            Assert.Equal(CatalogIndexScanState.Complete, scan.State);
            VerifyAssetRequests(type);
        }

        private void VerifyAssetRequests(CatalogScanDriverType type)
        {
            var expectedAssets = CatalogScanDriverMetadata.GetDownloadedPackageAssets(type);
            var assetToGetRequestCount = new Dictionary<DownloadedPackageAssets, Func<int>>
            {
                [DownloadedPackageAssets.Nupkg] = GetNupkgRequestCount,
                [DownloadedPackageAssets.Nuspec] = GetNuspecRequestCount,
                [DownloadedPackageAssets.Readme] = GetReadmeRequestCount,
                [DownloadedPackageAssets.License] = GetLicenseRequestCount,
                [DownloadedPackageAssets.Icon] = GetIconRequestCount,
                [DownloadedPackageAssets.Snupkg] = GetSnupkgRequestCount,
            };

            foreach (var assetType in Enum.GetValues<DownloadedPackageAssets>().Except([DownloadedPackageAssets.None]))
            {
                var requestCount = assetToGetRequestCount[assetType]();
                if (expectedAssets.HasFlag(assetType))
                {
                    Assert.True(requestCount > 0, $"There should be at least one {assetType} request for driver {type}.");
                }
                else
                {
                    Assert.True(requestCount == 0, $"There should be no {assetType} requests for driver {type}.");
                }
            }
        }

        public CatalogScanDriverMetadataIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}

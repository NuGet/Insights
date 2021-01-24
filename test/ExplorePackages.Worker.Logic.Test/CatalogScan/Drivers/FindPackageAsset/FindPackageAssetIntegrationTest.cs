using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.FindPackageAsset
{
    public class FindPackageAssetIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest
    {
        private const string FindPackageAssetDir = nameof(FindPackageAsset);
        private const string FindPackageAsset_WithDeleteDir = nameof(FindPackageAsset_WithDelete);
        private const string FindPackageAsset_WithDuplicatesDir = nameof(FindPackageAsset_WithDuplicates);

        public FindPackageAssetIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.PackageAssetContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.FindPackageAsset;

        public class FindPackageAsset : FindPackageAssetIntegrationTest
        {
            public FindPackageAsset(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");
                var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(FindPackageAssetDir, Step1, 0);
                await AssertOutputAsync(FindPackageAssetDir, Step1, 1);
                await AssertOutputAsync(FindPackageAssetDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(FindPackageAssetDir, Step2, 0);
                await AssertOutputAsync(FindPackageAssetDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(FindPackageAssetDir, Step2, 2);

                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class FindPackageAsset_WithoutBatching : FindPackageAssetIntegrationTest
        {
            public FindPackageAsset_WithoutBatching(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureWorkerSettings = x => x.AllowBatching = false;

                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(FindPackageAssetDir, Step1, 0);
                await AssertOutputAsync(FindPackageAssetDir, Step1, 1);
                await AssertOutputAsync(FindPackageAssetDir, Step1, 2);

                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class FindPackageAsset_WithDelete : FindPackageAssetIntegrationTest
        {
            public FindPackageAsset_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsolutePath.EndsWith("/behaviorsample.1.0.0.nupkg"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/behaviorsample.1.0.0.nupkg");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };
                var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z");
                var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z");
                var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(FindPackageAsset_WithDeleteDir, Step1, 0);
                await AssertOutputAsync(FindPackageAsset_WithDeleteDir, Step1, 1);
                await AssertOutputAsync(FindPackageAsset_WithDeleteDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(FindPackageAsset_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputAsync(FindPackageAsset_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(FindPackageAsset_WithDeleteDir, Step2, 2);

                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class FindPackageAsset_WithDuplicates_OnlyLatestLeaves : FindPackageAssetIntegrationTest
        {
            public FindPackageAsset_WithDuplicates_OnlyLatestLeaves(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            public override bool OnlyLatestLeaves => true;

            [Fact]
            public Task Execute()
            {
                return FindPackageAsset_WithDuplicates(batchProcessing: false);
            }
        }

        public class FindPackageAsset_WithDuplicates_AllLeaves : FindPackageAssetIntegrationTest
        {
            public FindPackageAsset_WithDuplicates_AllLeaves(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            public override bool OnlyLatestLeaves => false;

            [Fact]
            public async Task Execute()
            {
                await FindPackageAsset_WithDuplicates(batchProcessing: false);
            }
        }

        public class FindPackageAsset_WithDuplicates_BatchProcessing : FindPackageAssetIntegrationTest
        {
            public FindPackageAsset_WithDuplicates_BatchProcessing(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            public override bool OnlyLatestLeaves => false;

            [Fact]
            public Task Execute()
            {
                return FindPackageAsset_WithDuplicates(batchProcessing: true);
            }
        }

        private async Task FindPackageAsset_WithDuplicates(bool batchProcessing)
        {
            ConfigureWorkerSettings = x =>
            {
                x.AppendResultStorageBucketCount = 1;
                x.RunAllCatalogScanDriversAsBatch = batchProcessing;
            };

            Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(FindPackageAsset_WithDuplicatesDir, Step1, 0);

            var duplicatePackageRequests = HttpMessageHandlerFactory
                .Requests
                .Where(x => x.RequestUri.AbsolutePath.EndsWith("/gosms.ge-sms-api.1.0.1.nupkg"))
                .ToList();
            Assert.Equal(OnlyLatestLeaves ? 1 : 2, duplicatePackageRequests.Where(x => x.Method == HttpMethod.Head).Count());
            Assert.Equal(OnlyLatestLeaves ? 1 : 2, duplicatePackageRequests.Where(x => x.Method == HttpMethod.Get).Count());

            await AssertExpectedStorageAsync();
            AssertOnlyInfoLogsOrLess();
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageFileTableName });
        }
    }
}

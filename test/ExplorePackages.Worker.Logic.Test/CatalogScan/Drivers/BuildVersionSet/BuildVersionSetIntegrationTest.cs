using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.BuildVersionSet
{
    public class BuildVersionSetIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string BuildVersionSetDir = nameof(BuildVersionSet);
        private const string BuildVersionSet_WithDeleteDir = nameof(BuildVersionSet_WithDelete);
        private const string BuildVersionSet_WithDuplicatesDir = nameof(BuildVersionSet_WithDuplicates);

        public class BuildVersionSet : BuildVersionSetIntegrationTest
        {
            public BuildVersionSet(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2020-12-27T05:06:30.4180312Z");
                var max1 = DateTimeOffset.Parse("2020-12-27T05:07:21.9968244Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(BuildVersionSetDir, Step1);
                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class BuildVersionSet_WithDelete : BuildVersionSetIntegrationTest
        {
            public BuildVersionSet_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z");
                var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z");
                var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);
                var versionSet1 = await VersionSetService.ReadAsync();

                // Assert
                await AssertOutputAsync(BuildVersionSet_WithDeleteDir, Step1);

                Assert.True(versionSet1.DidIdEverExist("Nut.MediatR.ServiceLike.DependencyInjection"));
                Assert.True(versionSet1.DidVersionEverExist("Nut.MediatR.ServiceLike.DependencyInjection", "0.0.0-PREVIEW.0.44"));

                Assert.True(versionSet1.DidIdEverExist("BehaviorSample"));
                Assert.True(versionSet1.DidVersionEverExist("BehaviorSample", "1.0.0"));

                Assert.False(versionSet1.DidIdEverExist("doesnotexist"));
                Assert.False(versionSet1.DidVersionEverExist("doesnotexist", "1.0.0"));

                // Act
                await UpdateAsync(max2);
                var versionSet2 = await VersionSetService.ReadAsync();

                // Assert
                await AssertOutputAsync(BuildVersionSet_WithDeleteDir, Step2);

                Assert.True(versionSet2.DidIdEverExist("Nut.MediatR.ServiceLike.DependencyInjection"));
                Assert.True(versionSet2.DidVersionEverExist("Nut.MediatR.ServiceLike.DependencyInjection", "0.0.0-PREVIEW.0.44"));

                Assert.True(versionSet2.DidIdEverExist("BehaviorSample"));
                Assert.True(versionSet2.DidVersionEverExist("BehaviorSample", "1.0.0"));

                Assert.False(versionSet2.DidIdEverExist("doesnotexist"));
                Assert.False(versionSet2.DidVersionEverExist("doesnotexist", "1.0.0"));

                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class BuildVersionSet_WithDuplicates : BuildVersionSetIntegrationTest
        {
            public BuildVersionSet_WithDuplicates(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2019-01-24T15:03:56.0495104Z");
                var max1 = DateTimeOffset.Parse("2019-01-24T21:30:58.7012340Z");
                var max2 = DateTimeOffset.Parse("2019-01-25T01:00:01.5210470Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(BuildVersionSet_WithDuplicatesDir, Step1);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(BuildVersionSet_WithDuplicatesDir, Step2);
                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public BuildVersionSetIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.BuildVersionSet;
        public override bool OnlyLatestLeaves => false;
        public override bool OnlyLatestLeavesPerId => false;
        public VersionSetService VersionSetService => Host.Services.GetRequiredService<VersionSetService>();

        protected override IEnumerable<string> GetExpectedBlobContainerNames()
        {
            return base.GetExpectedBlobContainerNames().Concat(new[] { Options.Value.VersionSetContainerName });
        }

        protected async Task AssertOutputAsync(string testName, string stepName)
        {
            const string blobName = "version-set.dat";
            const string fileName = "data.json";

            var client = ServiceClientFactory.GetStorageAccount().CreateCloudBlobClient();
            var container = client.GetContainerReference(Options.Value.VersionSetContainerName);
            var blob = container.GetBlockBlobReference(blobName);

            using var memoryStream = new MemoryStream();
            await blob.DownloadToStreamAsync(memoryStream);
            var compactJson = MessagePackSerializer.ConvertToJson(memoryStream.ToArray(), ExplorePackagesMessagePack.Options);
            var parsedJson = JToken.Parse(compactJson);
            var actual = parsedJson.ToString();

            if (OverwriteTestData)
            {
                Directory.CreateDirectory(Path.Combine(TestData, testName, stepName));
                File.WriteAllText(Path.Combine(TestData, testName, stepName, fileName), actual);
            }
            var expected = File.ReadAllText(Path.Combine(TestData, testName, stepName, fileName));
            Assert.Equal(expected, actual);
        }
    }
}

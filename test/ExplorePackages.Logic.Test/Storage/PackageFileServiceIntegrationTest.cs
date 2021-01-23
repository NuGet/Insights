using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages
{
    public class PackageFileServiceIntegrationTest : BaseLogicIntegrationTest
    {
        [Fact]
        public async Task UpdatesDataForNewCatalogLeaf()
        {
            // Arrange
            await Target.InitializeAsync();

            var leafItem = new CatalogLeafItem
            {
                CommitId = "dc9945c1-9199-4479-bdea-22a6554d5b4d",
                CommitTimestamp = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z"),
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                Type = CatalogLeafType.PackageDetails,
                Url = "https://api.nuget.org/v3/catalog0/data/2018.10.15.01.04.00/newtonsoft.json.9.0.1.json",
            };

            var initial = await Target.GetOrUpdateInfoAsync(leafItem);
            var requestCount = HttpMessageHandlerFactory.Requests.Count;
            leafItem.CommitId = "different";

            // Act
            var updated = await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.Equal(2 * requestCount, HttpMessageHandlerFactory.Requests.Count);
            Assert.Equal("dc9945c1-9199-4479-bdea-22a6554d5b4d", initial.CommitId);
            Assert.Equal("different", updated.CommitId);
        }

        [Fact]
        public async Task DoesNotRepeatHttpRequestsForSameLeaf()
        {
            // Arrange
            await Target.InitializeAsync();

            var leafItem = new CatalogLeafItem
            {
                CommitId = "dc9945c1-9199-4479-bdea-22a6554d5b4d",
                CommitTimestamp = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z"),
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                Type = CatalogLeafType.PackageDetails,
                Url = "https://api.nuget.org/v3/catalog0/data/2018.10.15.01.04.00/newtonsoft.json.9.0.1.json",
            };

            await Target.GetOrUpdateInfoAsync(leafItem);
            var requestCount = HttpMessageHandlerFactory.Requests.Count;

            // Act
            await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.Equal(requestCount, HttpMessageHandlerFactory.Requests.Count);
        }

        public PackageFileServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public PackageFileService Target => Host.Services.GetRequiredService<PackageFileService>();
    }
}

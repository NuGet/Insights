using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public abstract class BaseCatalogScanToCsvIntegrationTest : BaseCatalogScanIntegrationTest
    {
        public BaseCatalogScanToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected abstract string DestinationContainerName { get; }

        protected override IEnumerable<string> GetExpectedBlobContainerNames()
        {
            yield return DestinationContainerName;
        }

        protected async Task AssertOutputAsync(string testName, string stepName, int bucket)
        {
            var client = ServiceClientFactory.GetStorageAccount().CreateCloudBlobClient();
            var container = client.GetContainerReference(DestinationContainerName);
            var blob = container.GetBlockBlobReference($"compact_{bucket}.csv");
            var actual = await blob.DownloadTextAsync();
            if (OverwriteTestData)
            {
                Directory.CreateDirectory(Path.Combine(TestData, testName, stepName));
                File.WriteAllText(Path.Combine(TestData, testName, stepName, blob.Name), actual);
            }
            var expected = File.ReadAllText(Path.Combine(TestData, testName, stepName, blob.Name));
            Assert.Equal(expected, actual);
        }
    }
}

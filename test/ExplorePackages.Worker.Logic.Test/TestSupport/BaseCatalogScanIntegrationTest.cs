using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public abstract class BaseCatalogScanIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public BaseCatalogScanIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected abstract string DestinationContainerName { get; }
        protected abstract CatalogScanType Type { get; }

        protected Task SetCursorAsync(DateTimeOffset min) => SetCursorAsync(Type, min);
        protected Task UpdateAsync(DateTimeOffset max) => UpdateAsync(Type, max);

        protected async Task VerifyExpectedContainersAsync()
        {
            var account = ServiceClientFactory.GetStorageAccount();

            var containers = await account.CreateCloudBlobClient().ListContainersAsync(StoragePrefix);
            Assert.Equal(
                new[] { Options.Value.LeaseContainerName, DestinationContainerName }.OrderBy(x => x).ToArray(),
                containers.Select(x => x.Name).ToArray());

            var queues = await account.CreateCloudQueueClient().ListQueuesAsync(StoragePrefix);
            Assert.Equal(
                new[] { Options.Value.WorkerQueueName, Options.Value.WorkerQueueName + "-poison" },
                queues.Select(x => x.Name).ToArray());

            var tables = await account.CreateCloudTableClient().ListTablesAsync(StoragePrefix);
            Assert.Equal(
                new[] { Options.Value.CursorTableName, Options.Value.CatalogIndexScanTableName },
                tables.Select(x => x.Name).ToArray());
        }

        protected async Task AssertOutputAsync(string testName, string stepName, int bucket)
        {
            var client = ServiceClientFactory.GetStorageAccount().CreateCloudBlobClient();
            var container = client.GetContainerReference(DestinationContainerName);
            var blob = container.GetBlockBlobReference($"compact_{bucket}.csv");
            var actual = await blob.DownloadTextAsync();
            // Directory.CreateDirectory(Path.Combine(TestData, testName, stepName));
            // File.WriteAllText(Path.Combine(TestData, testName, stepName, blob.Name), actual);
            var expected = File.ReadAllText(Path.Combine(TestData, testName, stepName, blob.Name));
            Assert.Equal(expected, actual);
        }
    }
}

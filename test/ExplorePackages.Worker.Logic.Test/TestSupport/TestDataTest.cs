using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public class TestDataTest : BaseWorkerLogicIntegrationTest
    {
        public TestDataTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public void IsNotOverwritingTestData()
        {
            Assert.False(OverwriteTestData);
        }

        [Fact]
        public async Task DeleteOldContainers()
        {
            // Clean up
            var account = ServiceClientFactory.GetStorageAccount();

            var blobServiceClient = await NewServiceClientFactory.GetBlobServiceClientAsync();
            var containerItems = await blobServiceClient.GetBlobContainersAsync().ToListAsync();
            foreach (var containerItem in containerItems.Where(x => IsOldStoragePrefix(x.Name)))
            {
                Logger.LogInformation("Deleting old container: {Name}", containerItem.Name);
                await blobServiceClient.DeleteBlobContainerAsync(containerItem.Name);
            }

            var queueServiceClient = await NewServiceClientFactory.GetQueueServiceClientAsync();
            var queueItems = await queueServiceClient.GetQueuesAsync().ToListAsync();
            foreach (var queueItem in queueItems.Where(x => IsOldStoragePrefix(x.Name)))
            {
                Logger.LogInformation("Deleting old queue: {Name}", queueItem.Name);
                await queueServiceClient.DeleteQueueAsync(queueItem.Name);
            }

            var tables = await account.CreateCloudTableClient().ListTablesAsync(string.Empty);
            foreach (var table in tables.Where(x => IsOldStoragePrefix(x.Name)))
            {
                Logger.LogInformation("Deleting old table: {Name}", table.Name);
                await table.DeleteAsync();
            }
        }

        private bool IsOldStoragePrefix(string name)
        {
            var match = StoragePrefixPattern.Match(name);
            if (!match.Success)
            {
                return false;
            }

            var date = DateTimeOffset.ParseExact(match.Groups["Date"].Value, "yyMMdd", CultureInfo.InvariantCulture);
            if (DateTimeOffset.UtcNow - date < TimeSpan.FromDays(2))
            {
                return false;
            }

            return true;
        }
    }
}

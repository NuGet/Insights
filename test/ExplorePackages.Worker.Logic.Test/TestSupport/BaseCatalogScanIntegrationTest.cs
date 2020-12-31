using System;
using System.Collections.Generic;
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

        protected abstract CatalogScanType Type { get; }

        protected Task SetCursorAsync(DateTimeOffset min) => SetCursorAsync(Type, min);
        protected Task<CatalogIndexScan> UpdateAsync(DateTimeOffset max) => UpdateAsync(Type, max);

        protected async Task VerifyExpectedContainersAsync()
        {
            var account = ServiceClientFactory.GetStorageAccount();

            var containers = await account.CreateCloudBlobClient().ListContainersAsync(StoragePrefix);
            Assert.Equal(
                GetExpectedBlobContainerNames().Concat(new[] { Options.Value.LeaseContainerName }).OrderBy(x => x).ToArray(),
                containers.Select(x => x.Name).ToArray());

            var queues = await account.CreateCloudQueueClient().ListQueuesAsync(StoragePrefix);
            Assert.Equal(
                new[] { Options.Value.WorkerQueueName, Options.Value.WorkerQueueName + "-poison" },
                queues.Select(x => x.Name).ToArray());

            var tables = await account.CreateCloudTableClient().ListTablesAsync(StoragePrefix);
            Assert.Equal(
                GetExpectedTableNames().Concat(new[] { Options.Value.CursorTableName, Options.Value.CatalogIndexScanTableName }).OrderBy(x => x).ToArray(),
                tables.Select(x => x.Name).ToArray());
        }

        protected virtual IEnumerable<string> GetExpectedBlobContainerNames()
        {
            yield break;
        }

        protected virtual IEnumerable<string> GetExpectedTableNames()
        {
            yield break;
        }
    }
}

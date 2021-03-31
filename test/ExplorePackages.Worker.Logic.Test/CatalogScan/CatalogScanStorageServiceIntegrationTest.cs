using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanStorageServiceIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public class TheDeleteOlderIndexScansAsyncMethod : CatalogScanStorageServiceIntegrationTest
        {
            [Fact]
            public async Task DeletesOlderCompletedScans()
            {
                ConfigureWorkerSettings = x => x.OldCatalogIndexScansToKeep = 3;

                await Target.InitializeAsync();
                var scans = Enumerable
                    .Range(0, 10)
                    .Select(x => new CatalogIndexScan(CursorName, StorageUtility.GenerateDescendingId().ToString(), StorageSuffix)
                    {
                        State = CatalogIndexScanState.Complete,
                    })
                    .OrderBy(x => x.GetScanId())
                    .ToList();
                await Task.WhenAll(scans.Select(x => Target.InsertAsync(x)));
                var currentScanId = scans.Skip(3).First().GetScanId();

                await Target.DeleteOldIndexScansAsync(CursorName, currentScanId);

                var remainingScans = await (await ServiceClientFactory.GetTableServiceClientAsync())
                    .GetTableClient(Options.Value.CatalogIndexScanTableName)
                    .QueryAsync<CatalogIndexScan>()
                    .ToListAsync();
                Assert.Equal(scans.Take(7).Select(x => x.RowKey).ToList(), remainingScans.Select(x => x.RowKey).ToList());
            }

            [Fact]
            public async Task DoesNotDeleteIncompleteScans()
            {
                ConfigureWorkerSettings = x => x.OldCatalogIndexScansToKeep = 0;

                await Target.InitializeAsync();
                var scans = Enumerable
                    .Range(0, 3)
                    .Select(x => new CatalogIndexScan(CursorName, StorageUtility.GenerateDescendingId().ToString(), StorageSuffix)
                    {
                        State = CatalogIndexScanState.Created,
                    })
                    .OrderBy(x => x.GetScanId())
                    .ToList();
                await Task.WhenAll(scans.Select(x => Target.InsertAsync(x)));
                var currentScanId = scans.First().GetScanId();

                await Target.DeleteOldIndexScansAsync(CursorName, currentScanId);

                var remainingScans = await (await ServiceClientFactory.GetTableServiceClientAsync())
                    .GetTableClient(Options.Value.CatalogIndexScanTableName)
                    .QueryAsync<CatalogIndexScan>()
                    .ToListAsync();
                Assert.Equal(scans.Select(x => x.RowKey).ToList(), remainingScans.Select(x => x.RowKey).ToList());
            }

            public TheDeleteOlderIndexScansAsyncMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public CatalogScanStorageServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            CursorName = "foo";
            StorageSuffix = "bar";
        }

        public CatalogScanStorageService Target => CatalogScanStorageService;
        public string CursorName { get; }
        public string StorageSuffix { get; }
    }
}

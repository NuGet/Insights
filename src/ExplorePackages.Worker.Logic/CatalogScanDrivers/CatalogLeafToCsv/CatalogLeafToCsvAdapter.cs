using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafToCsvAdapter<T> : ICatalogScanDriver where T : ICsvRecord<T>
    {
        private readonly AppendResultStorageService _storageService;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly SchemaSerializer _schemaSerializer;
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly ICatalogLeafToCsvDriver<T> _driver;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<CatalogLeafToCsvAdapter<T>> _logger;

        public CatalogLeafToCsvAdapter(
            AppendResultStorageService storageService,
            TaskStateStorageService taskStateStorageService,
            SchemaSerializer schemaSerializer,
            MessageEnqueuer messageEnqueuer,
            ICatalogLeafToCsvDriver<T> driver,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<CatalogLeafToCsvAdapter<T>> logger)
        {
            _storageService = storageService;
            _taskStateStorageService = taskStateStorageService;
            _schemaSerializer = schemaSerializer;
            _messageEnqueuer = messageEnqueuer;
            _driver = driver;
            _options = options;
            _logger = logger;
        }

        public async Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            await _storageService.InitializeAsync(GetTableName(indexScan.StorageSuffix), _driver.ResultsContainerName);
            await _taskStateStorageService.InitializeAsync(indexScan.StorageSuffix);

            return CatalogIndexScanResult.Expand;
        }

        public Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            return Task.FromResult(CatalogPageScanResult.Expand);
        }

        public async Task ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var leafItem = new CatalogLeafItem
            {
                Url = leafScan.Url,
                Type = leafScan.ParsedLeafType,
                CommitId = leafScan.CommitId,
                CommitTimestamp = leafScan.CommitTimestamp,
                PackageId = leafScan.PackageId,
                PackageVersion = leafScan.PackageVersion
            };

            var records = await _driver.ProcessLeafAsync(leafItem);
            if (!records.Any())
            {
                return;
            }

            var bucketKey = $"{leafScan.PackageId}/{NuGetVersion.Parse(leafScan.PackageVersion).ToNormalizedString()}".ToLowerInvariant();
            var parameters = (CatalogLeafToCsvParameters)_schemaSerializer.Deserialize(leafScan.ScanParameters);
            await _storageService.AppendAsync(GetTableName(leafScan.StorageSuffix), parameters.BucketCount, bucketKey, records);
        }

        public async Task StartAggregateAsync(CatalogIndexScan indexScan)
        {
            var buckets = await _storageService.GetWrittenBucketsAsync(GetTableName(indexScan.StorageSuffix));

            var partitionKey = GetAggregateTasksPartitionKey(indexScan);

            await _taskStateStorageService.InitializeAllAsync(
                indexScan.StorageSuffix,
                partitionKey,
                buckets.Select(x => x.ToString()).ToList());

            var messages = buckets
                .Select(b => new CatalogLeafToCsvCompactMessage<T>
                {
                    SourceContainer = GetTableName(indexScan.StorageSuffix),
                    Bucket = b,
                    TaskStateStorageSuffix = indexScan.StorageSuffix,
                    TaskStatePartitionKey = partitionKey,
                    TaskStateRowKey = b.ToString(),
                })
                .ToList();
            await _messageEnqueuer.EnqueueAsync(messages);
        }

        public async Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan)
        {
            var countLowerBound = await _taskStateStorageService.GetCountLowerBoundAsync(
                indexScan.StorageSuffix,
                GetAggregateTasksPartitionKey(indexScan));
            _logger.LogInformation("There are at least {Count} compact tasks pending.", countLowerBound);
            return countLowerBound == 0;
        }

        private static string GetAggregateTasksPartitionKey(CatalogIndexScan indexScan)
        {
            return $"{indexScan.ScanId}-aggregate";
        }

        public async Task FinalizeAsync(CatalogIndexScan indexScan)
        {
            if (!string.IsNullOrEmpty(indexScan.StorageSuffix))
            {
                await _taskStateStorageService.DeleteTableAsync(indexScan.StorageSuffix);
                await _storageService.DeleteAsync(GetTableName(indexScan.StorageSuffix));
            }
        }

        private string GetTableName(string suffix)
        {
            return $"{_options.Value.CatalogLeafToCsvTableName}{suffix}";
        }
    }
}

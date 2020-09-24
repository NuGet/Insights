using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogScanService
    {
        private readonly CatalogClient _catalogClient;
        private readonly CursorStorageService _cursorStorageService;
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly SchemaSerializer _serializer;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly ILogger<CatalogScanService> _logger;

        public CatalogScanService(
            CatalogClient catalogClient,
            CursorStorageService cursorStorageService,
            MessageEnqueuer messageEnqueuer,
            SchemaSerializer serializer,
            CatalogScanStorageService catalogScanStorageService,
            ILogger<CatalogScanService> logger)
        {
            _catalogClient = catalogClient;
            _cursorStorageService = cursorStorageService;
            _messageEnqueuer = messageEnqueuer;
            _serializer = serializer;
            _catalogScanStorageService = catalogScanStorageService;
            _logger = logger;
        }

        public async Task<CatalogIndexScan> UpdateAsync()
        {
            // Determine the bounds of the scan.
            var cursor = await _cursorStorageService.GetOrCreateAsync("CatalogScan");
            var index = await _catalogClient.GetCatalogIndexAsync();
            var min = new[] { cursor.Value, CursorService.NuGetOrgMin }.Max();
            var max = index.CommitTimestamp;

            if (min == max)
            {
                return null;
            }

            // Start a new scan.
            _logger.LogInformation("Attempting to start a catalog index scan from ({Min}, {Max}].", min, max);
            var scanId = TableStorageUtility.GenerateDescendingId();
            var catalogIndexScanMessage = new CatalogIndexScanMessage { ScanId = scanId };
            await _messageEnqueuer.EnqueueAsync(new[] { catalogIndexScanMessage });

            var catalogIndexScan = new CatalogIndexScan(scanId)
            {
                ParsedScanType = CatalogScanType.FindLatestLeaves,
                ScanParameters = _serializer.Serialize(new FindLatestLeavesParameters { Prefix = string.Empty }).AsString(),
                ParsedState = CatalogScanState.Created,
                Min = min,
                Max = max,
                CursorName = cursor.Name,
            };
            await _catalogScanStorageService.InsertAsync(catalogIndexScan);

            return catalogIndexScan;
        }
    }
}

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogLeafScanMessageProcessor : IMessageProcessor<CatalogLeafScanMessage>
    {
        private readonly CatalogScanDriverFactory _driverFactory;
        private readonly CatalogScanStorageService _storageService;
        private readonly ILogger<CatalogLeafScanMessageProcessor> _logger;

        public CatalogLeafScanMessageProcessor(
            CatalogScanDriverFactory driverFactory,
            CatalogScanStorageService storageService,
            ILogger<CatalogLeafScanMessageProcessor> logger)
        {
            _driverFactory = driverFactory;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task ProcessAsync(CatalogLeafScanMessage message)
        {
            var scan = await _storageService.GetLeafScanAsync(message.StorageSuffix, message.ScanId, message.PageId, message.LeafId);
            if (scan == null)
            {
                _logger.LogWarning("No matching leaf scan was found.");
                return;
            }

            var driver = _driverFactory.Create(scan.ParsedScanType);

            await driver.ProcessLeafAsync(scan);

            await _storageService.DeleteAsync(scan);
        }
    }
}

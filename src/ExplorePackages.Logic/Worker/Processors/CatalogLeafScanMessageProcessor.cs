using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogLeafScanMessageProcessor : IMessageProcessor<CatalogLeafScanMessage>
    {
        private readonly CatalogScanDriverFactory _driverFactory;
        private readonly CatalogScanStorageService _storageService;

        public CatalogLeafScanMessageProcessor(
            CatalogScanDriverFactory driverFactory,
            CatalogScanStorageService storageService)
        {
            _driverFactory = driverFactory;
            _storageService = storageService;
        }

        public async Task ProcessAsync(CatalogLeafScanMessage message)
        {
            var scan = await _storageService.GetLeafScanAsync(message.ScanId, message.PageId, message.LeafId);
            if (scan == null)
            {
                return;
            }

            var driver = _driverFactory.Create(scan.ParsedScanType);

            await driver.ProcessLeafAsync(scan);

            await _storageService.DeleteAsync(scan);
        }
    }
}

using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogLeafMessageProcessor : IMessageProcessor<CatalogLeafMessage>
    {
        private readonly CatalogScanDriverFactory _driverFactory;
        private readonly CatalogScanStorageService _storageService;

        public CatalogLeafMessageProcessor(
            CatalogScanDriverFactory driverFactory,
            CatalogScanStorageService storageService)
        {
            _driverFactory = driverFactory;
            _storageService = storageService;
        }

        public async Task ProcessAsync(CatalogLeafMessage message)
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

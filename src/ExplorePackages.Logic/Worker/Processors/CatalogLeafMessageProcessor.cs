using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogLeafMessageProcessor : IMessageProcessor<CatalogLeafMessage>
    {
        private readonly CatalogClient _catalogClient;
        private readonly ILogger<CatalogLeafMessageProcessor> _logger;

        public CatalogLeafMessageProcessor(
            CatalogClient catalogClient,
            ILogger<CatalogLeafMessageProcessor> logger)
        {
            _catalogClient = catalogClient;
            _logger = logger;
        }

        public async Task ProcessAsync(CatalogLeafMessage message)
        {
            if (message.ScanType == CatalogScanType.DownloadLeaves)
            {
                _logger.LogInformation("Loading catalog {Type} leaf URL: {Url}", message.LeafType, message.Url);
                await _catalogClient.GetCatalogLeafAsync(message.LeafType, message.Url);
            }
        }
    }
}

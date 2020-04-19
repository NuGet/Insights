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
            _logger.LogInformation("Loading catalog {Type} leaf URL: {Url}", message.Type, message.Url);
            await _catalogClient.GetCatalogLeafAsync(message.Type, message.Url);
        }
    }
}

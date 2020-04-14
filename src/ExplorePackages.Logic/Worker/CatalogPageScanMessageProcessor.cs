using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogPageScanMessageProcessor : IMessageProcessor<CatalogPageScanMessage>
    {
        private readonly CatalogClient _catalogClient;

        public CatalogPageScanMessageProcessor(CatalogClient catalogClient)
        {
            _catalogClient = catalogClient;
        }

        public async Task ProcessAsync(CatalogPageScanMessage message)
        {
            await _catalogClient.GetCatalogPageAsync(message.Url);
        }
    }
}

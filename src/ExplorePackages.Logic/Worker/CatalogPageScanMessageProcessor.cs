using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogPageScanMessageProcessor : IMessageProcessor<CatalogPageScanMessage>
    {
        private readonly CatalogClient _catalogClient;
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly ILogger<CatalogPageScanMessageProcessor> _logger;

        public CatalogPageScanMessageProcessor(
            CatalogClient catalogClient,
            MessageEnqueuer messageEnqueuer,
            ILogger<CatalogPageScanMessageProcessor> logger)
        {
            _catalogClient = catalogClient;
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task ProcessAsync(CatalogPageScanMessage message)
        {
            _logger.LogInformation("Loading catalog page URL: {Url}", message.Url);
            var page = await _catalogClient.GetCatalogPageAsync(message.Url);

            var leaves = page.GetLeavesInBounds(message.Min, message.Max, excludeRedundantLeaves: false);

            _logger.LogInformation("Starting scan of {LeafCount} leaves from ({Min:O}, {Max:O}].", leaves.Count, message.Min, message.Max);
            await _messageEnqueuer.EnqueueAsync(leaves
                .Select(x => new CatalogLeafMessage { Type = x.Type, Url = x.Url })
                .ToList());
        }
    }
}

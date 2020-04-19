using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogIndexScanMessageProcessor : IMessageProcessor<CatalogIndexScanMessage>
    {
        private readonly CatalogClient _catalogClient;
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly ILogger<CatalogIndexScanMessageProcessor> _logger;

        public CatalogIndexScanMessageProcessor(
            CatalogClient catalogClient,
            MessageEnqueuer messageEnqueuer,
            ILogger<CatalogIndexScanMessageProcessor> logger)
        {
            _catalogClient = catalogClient;
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task ProcessAsync(CatalogIndexScanMessage message)
        {
            _logger.LogInformation("Loading catalog index.");
            var catalogIndex = await _catalogClient.GetCatalogIndexAsync();

            var min = message.Min ?? CursorService.NuGetOrgMin;
            var max = new[] { message.Max ?? DateTimeOffset.MaxValue, catalogIndex.CommitTimestamp }.Min();
            var pages = catalogIndex.GetPagesInBounds(min, max);

            _logger.LogInformation("Starting scan of {PageCount} pages from ({Min:O}, {Max:O}].", pages.Count, min, max);
            await _messageEnqueuer.EnqueueAsync(pages
                .Select(x => new CatalogPageScanMessage
                {
                    Url = x.Url,
                    Min = min,
                    Max = max
                })
                .ToList());
        }
    }
}

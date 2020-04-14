using System;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogIndexScanMessageProcessor : IMessageProcessor<CatalogIndexScanMessage>
    {
        private readonly CatalogClient _catalogClient;
        private readonly IMessageEnqueuer _messageEnqueuer;

        public CatalogIndexScanMessageProcessor(CatalogClient catalogClient, IMessageEnqueuer messageEnqueuer)
        {
            _catalogClient = catalogClient;
            _messageEnqueuer = messageEnqueuer;
        }

        public async Task ProcessAsync(CatalogIndexScanMessage message)
        {
            var catalogIndex = await _catalogClient.GetCatalogIndexAsync();

            var min = message.Min ?? CursorService.NuGetOrgMin;
            var max = new[] { message.Max ?? DateTimeOffset.MaxValue, catalogIndex.CommitTimestamp }.Min();
            var pages = catalogIndex.GetPagesInBounds(min, max);
            
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

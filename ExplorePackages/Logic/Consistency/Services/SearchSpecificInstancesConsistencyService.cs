using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchSpecificInstancesConsistencyService : SearchConsistencyService
    {
        public SearchSpecificInstancesConsistencyService(
            SearchServiceUrlDiscoverer discoverer,
            SearchClient searchClient,
            ILogger<SearchSpecificInstancesConsistencyService> logger) : base(
                discoverer,
                searchClient,
                logger,
                specificInstances: true)
        {
        }
    }
}

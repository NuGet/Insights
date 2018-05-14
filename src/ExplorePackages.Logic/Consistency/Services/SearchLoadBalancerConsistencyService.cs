using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchLoadBalancerConsistencyService : SearchConsistencyService
    {
        public SearchLoadBalancerConsistencyService(
            SearchServiceUrlDiscoverer discoverer,
            SearchClient searchClient,
            ILogger<SearchLoadBalancerConsistencyService> logger) : base(
                discoverer,
                searchClient,
                logger,
                specificInstances: false)
        {
        }
    }
}

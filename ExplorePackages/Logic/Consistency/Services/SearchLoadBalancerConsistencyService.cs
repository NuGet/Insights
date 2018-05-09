using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchLoadBalancerConsistencyService : SearchConsistencyService
    {
        public SearchLoadBalancerConsistencyService(
            SearchServiceUrlDiscoverer discoverer,
            SearchClient searchClient,
            ILogger log) : base(
                discoverer,
                searchClient,
                log,
                specificInstances: false)
        {
        }
    }
}

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchLoadBalancerConsistencyService : SearchConsistencyService
    {
        public SearchLoadBalancerConsistencyService(
            SearchServiceUrlDiscoverer discoverer,
            SearchClient searchClient) : base(
                discoverer,
                searchClient,
                specificInstances: false)
        {
        }
    }
}

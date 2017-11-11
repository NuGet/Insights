namespace Knapcode.ExplorePackages.Logic
{
    public class SearchSpecificInstancesConsistencyService : SearchConsistencyService
    {
        public SearchSpecificInstancesConsistencyService(
            SearchServiceUrlDiscoverer discoverer,
            SearchClient searchClient) : base(
                discoverer,
                searchClient,
                specificInstances: true)
        {
        }
    }
}

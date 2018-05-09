using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchSpecificInstancesConsistencyService : SearchConsistencyService
    {
        public SearchSpecificInstancesConsistencyService(
            SearchServiceUrlDiscoverer discoverer,
            SearchClient searchClient,
            ILogger log) : base(
                discoverer,
                searchClient,
                log,
                specificInstances: true)
        {
        }
    }
}

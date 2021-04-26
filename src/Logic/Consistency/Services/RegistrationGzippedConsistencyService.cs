namespace Knapcode.ExplorePackages
{
    public class RegistrationGzippedConsistencyService : RegistrationConsistencyService
    {
        public RegistrationGzippedConsistencyService(ServiceIndexCache serviceIndexCache, RegistrationClient client)
            : base(serviceIndexCache, client, type: ServiceIndexTypes.RegistrationGzipped, hasSemVer2: false)
        {
        }
    }
}

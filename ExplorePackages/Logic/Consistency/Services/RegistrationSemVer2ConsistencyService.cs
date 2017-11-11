namespace Knapcode.ExplorePackages.Logic
{
    public class RegistrationSemVer2ConsistencyService : RegistrationConsistencyService
    {
        public RegistrationSemVer2ConsistencyService(ServiceIndexCache serviceIndexCache, RegistrationClient client)
            : base(serviceIndexCache, client, type: ServiceIndexTypes.RegistrationSemVer2, hasSemVer2: true)
        {
        }
    }
}

namespace Knapcode.ExplorePackages.Logic
{
    public class RegistrationGzippedConsistencyService : RegistrationConsistencyService
    {
        public RegistrationGzippedConsistencyService(ServiceIndexCache serviceIndexCache, RegistrationClient client)
            : base(serviceIndexCache, client, type: "RegistrationsBaseUrl/3.4.0", hasSemVer2: false)
        {
        }
    }
}

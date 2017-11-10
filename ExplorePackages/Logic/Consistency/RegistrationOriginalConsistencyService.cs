namespace Knapcode.ExplorePackages.Logic
{
    public class RegistrationOriginalConsistencyService : RegistrationConsistencyService
    {
        public RegistrationOriginalConsistencyService(ServiceIndexCache serviceIndexCache, RegistrationClient client)
            : base(serviceIndexCache, client, type: "RegistrationsBaseUrl", hasSemVer2: false)
        {
        }
    }
}

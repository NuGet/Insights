namespace NuGet.Insights
{
    public class RegistrationOriginalConsistencyService : RegistrationConsistencyService
    {
        public RegistrationOriginalConsistencyService(ServiceIndexCache serviceIndexCache, RegistrationClient client)
            : base(serviceIndexCache, client, type: ServiceIndexTypes.RegistrationOriginal, hasSemVer2: false)
        {
        }
    }
}

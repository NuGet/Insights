namespace Knapcode.ExplorePackages.Logic
{
    public class HasRegistrationDiscrepancyInSemVer2HivePackageQuery : HasRegistrationDiscrepancyPackageQuery
    {
        public HasRegistrationDiscrepancyInSemVer2HivePackageQuery(
            ServiceIndexCache serviceIndexCache,
            RegistrationService registrationService)
            : base(
                  serviceIndexCache,
                  registrationService,
                  PackageQueryNames.HasRegistrationDiscrepancyInSemVer2HivePackageQuery,
                  CursorNames.HasRegistrationDiscrepancyInSemVer2HivePackageQuery,
                  registrationType: "RegistrationsBaseUrl/3.6.0",
                  hasSemVer2: true)
        {
        }
    }
}

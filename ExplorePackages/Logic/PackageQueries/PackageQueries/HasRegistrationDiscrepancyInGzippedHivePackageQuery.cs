namespace Knapcode.ExplorePackages.Logic
{
    public class HasRegistrationDiscrepancyInGzippedHivePackageQuery : HasRegistrationDiscrepancyPackageQuery
    {
        public HasRegistrationDiscrepancyInGzippedHivePackageQuery(
            ServiceIndexCache serviceIndexCache,
            RegistrationClient registrationService)
            : base(
                  serviceIndexCache,
                  registrationService,
                  PackageQueryNames.HasRegistrationDiscrepancyInGzippedHivePackageQuery,
                  CursorNames.HasRegistrationDiscrepancyInGzippedHivePackageQuery,
                  type: "RegistrationsBaseUrl/3.4.0",
                  hasSemVer2: false)
        {
        }
    }
}

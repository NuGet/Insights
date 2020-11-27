using System;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public static class PackageEntityExtensions
    {
        public static PackageConsistencyContext ToConsistencyContext(this PackageEntity package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.PackageRegistration == null)
            {
                throw new ArgumentException($"The package's {nameof(package.PackageRegistration)} property must not be null.", nameof(package));
            }

            var id = package.PackageRegistration.Id;
            var version = package.Version;
            var isDeleted = package.CatalogPackage?.Deleted ?? false;
            var isListed = package.CatalogPackage?.Listed ?? package.V2Package?.Listed ?? true;

            var isSemVer2 = false;
            if (package.CatalogPackage?.SemVerType != null)
            {
                isSemVer2 = package.CatalogPackage.SemVerType.Value != SemVerType.SemVer1;
            }

            return new PackageConsistencyContext(
                id,
                version,
                isDeleted,
                isListed,
                isSemVer2,
                hasIcon: false);
        }
    }
}

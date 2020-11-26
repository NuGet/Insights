using System;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageConsistencyContext
    {
        public PackageConsistencyContext(
            string id,
            string version,
            bool isDeleted,
            bool isSemVer2,
            bool isListed,
            bool hasIcon)
        {
            Id = id;
            Version = version;
            IsDeleted = isDeleted;
            IsSemVer2 = isSemVer2;
            IsListed = isListed;
            HasIcon = hasIcon;
        }

        public PackageConsistencyContext(PackageEntity package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.PackageRegistration == null)
            {
                throw new ArgumentException($"The package's {nameof(package.PackageRegistration)} property must not be null.", nameof(package));
            }

            Id = package.PackageRegistration.Id;
            Version = package.Version;
            IsDeleted = package.CatalogPackage?.Deleted ?? false;
            IsListed = package.CatalogPackage?.Listed ?? package.V2Package?.Listed ?? true;

            IsSemVer2 = false;
            if (package.CatalogPackage?.SemVerType != null)
            {
                IsSemVer2 = package.CatalogPackage.SemVerType.Value != SemVerType.SemVer1;
            }
        }

        public string Id { get; }
        public string Version { get; }
        public bool IsDeleted { get; }
        public bool IsListed { get; }
        public bool HasIcon { get; }
        public bool IsSemVer2 { get; }
    }
}

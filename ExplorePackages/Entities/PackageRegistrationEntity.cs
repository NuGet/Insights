using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities
{
    public class PackageRegistrationEntity
    {
        public long PackageRegistrationKey { get; set; }
        public string Id { get; set; }

        public List<PackageEntity> Packages { get; set; }
        public List<PackageDependencyEntity> PackageDependents { get; set; }
    }
}

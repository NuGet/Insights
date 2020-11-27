using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities
{
    public class PackageQueryMatches
    {
        public PackageQueryMatches(long lastKey, IReadOnlyList<PackageEntity> packages)
        {
            LastKey = lastKey;
            Packages = packages;
        }

        public long LastKey { get; }
        public IReadOnlyList<PackageEntity> Packages { get; }
    }
}

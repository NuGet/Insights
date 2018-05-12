using System.Collections.Generic;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
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

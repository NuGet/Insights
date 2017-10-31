using System.Collections.Generic;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryMatches
    {
        public PackageQueryMatches(long lastKey, IReadOnlyList<Package> packages)
        {
            LastKey = lastKey;
            Packages = packages;
        }

        public long LastKey { get; }
        public IReadOnlyList<Package> Packages { get; }
    }
}

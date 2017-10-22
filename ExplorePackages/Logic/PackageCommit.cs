using System;
using System.Collections.Generic;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageCommit
    {
        public PackageCommit(DateTimeOffset commitTimestamp, IReadOnlyList<Package> packages)
        {
            CommitTimestamp = commitTimestamp;
            Packages = packages;
        }

        public DateTimeOffset CommitTimestamp { get; }
        public IReadOnlyList<Package> Packages { get; }
    }
}

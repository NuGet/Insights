using System;

namespace Knapcode.ExplorePackages.VersionSets
{
    public interface IVersionSet
    {
        DateTimeOffset CommitTimestamp { get; }
        bool IsPackageAvaiable(string id, string version);
    }
}

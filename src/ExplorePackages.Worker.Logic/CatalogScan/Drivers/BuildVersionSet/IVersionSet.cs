using System;

namespace Knapcode.ExplorePackages.Worker.BuildVersionSet
{
    public interface IVersionSet
    {
        DateTimeOffset CommitTimestamp { get; }
        bool IsPackageAvaiable(string id, string version);
    }
}

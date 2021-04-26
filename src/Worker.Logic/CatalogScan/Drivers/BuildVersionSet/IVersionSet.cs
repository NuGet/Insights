using System;

namespace Knapcode.ExplorePackages.Worker.BuildVersionSet
{
    public interface IVersionSet
    {
        DateTimeOffset CommitTimestamp { get; }
        bool DidIdEverExist(string id);
        bool DidVersionEverExist(string id, string version);
    }
}

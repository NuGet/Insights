using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Worker.BuildVersionSet
{
    public interface IVersionSet
    {
        DateTimeOffset CommitTimestamp { get; }
        IReadOnlyCollection<string> GetUncheckedIds();
        IReadOnlyCollection<string> GetUncheckedVersions(string id);
        bool DidIdEverExist(string id);
        bool DidVersionEverExist(string id, string version);
    }
}

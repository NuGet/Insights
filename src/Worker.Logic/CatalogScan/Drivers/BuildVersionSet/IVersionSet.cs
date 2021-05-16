using System;
using System.Collections.Generic;

namespace NuGet.Insights.Worker.BuildVersionSet
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

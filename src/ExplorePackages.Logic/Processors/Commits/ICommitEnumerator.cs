using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface ICommitEnumerator<T>
    {
        Task<IReadOnlyList<EntityCommit<T>>> GetCommitsAsync(
            DateTimeOffset start,
            DateTimeOffset end,
            int batchSize);
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IPackageCommitEnumerator : ICommitEnumerator<PackageEntity>
    {
        Task<IReadOnlyList<EntityCommit<PackageEntity>>> GetCommitsAsync(
            QueryEntities<PackageEntity> queryEntities,
            DateTimeOffset start,
            DateTimeOffset end,
            int batchSize);
    }
}

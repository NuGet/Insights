using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages
{
    public class EntityCommit<T>
    {
        public EntityCommit(DateTimeOffset commitTimestamp, IReadOnlyList<T> entities)
        {
            CommitTimestamp = commitTimestamp;
            Entities = entities;
        }

        public DateTimeOffset CommitTimestamp { get; }
        public IReadOnlyList<T> Entities { get; }
    }
}

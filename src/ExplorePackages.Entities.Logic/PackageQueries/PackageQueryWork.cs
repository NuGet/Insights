using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities
{
    public class PackageQueryWork
    {
        public PackageQueryWork(IReadOnlyList<IPackageQuery> queries, PackageQueryContext context, PackageConsistencyState state)
        {
            Queries = queries;
            Context = context;
            State = state;
        }

        public IReadOnlyList<IPackageQuery> Queries { get; }
        public PackageQueryContext Context { get; }
        public PackageConsistencyState State { get; }
    }
}

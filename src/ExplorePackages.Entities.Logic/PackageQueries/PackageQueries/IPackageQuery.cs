using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Entities
{
    public interface IPackageQuery
    {
        string Name { get; }
        string CursorName { get; }
        bool NeedsNuspec { get; }
        bool NeedsMZip { get; }
        bool IsV2Query { get; }
        TimeSpan Delay { get; }
        Task<bool> IsMatchAsync(PackageQueryContext context, PackageConsistencyState state);
    }
}

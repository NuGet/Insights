using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IPackageQuery
    {
        string Name { get; }
        string CursorName { get; }
        bool NeedsNuspec { get; }
        bool NeedsMZip { get; }
        bool IsV2Query { get; }
        Task<bool> IsMatchAsync(PackageQueryContext context, PackageConsistencyState state);
    }
}

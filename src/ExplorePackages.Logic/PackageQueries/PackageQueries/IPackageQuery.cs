using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IPackageQuery
    {
        string Name { get; }
        string CursorName { get; }
        Task<bool> IsMatchAsync(PackageQueryContext context, PackageConsistencyState state);
    }
}

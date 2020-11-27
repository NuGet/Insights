using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Entities
{
    public interface IPackageConsistencyQuery
    {
        string Name { get; }
        string CursorName { get; }
        Task<bool> IsMatchAsync(PackageConsistencyContext context, PackageConsistencyState state);
    }
}

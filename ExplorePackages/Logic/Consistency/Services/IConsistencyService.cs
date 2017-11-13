using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IConsistencyService<T> where T : IConsistencyReport
    {
        Task<T> GetReportAsync(PackageQueryContext context, PackageConsistencyState state, IProgressReport progressReport);
        Task<bool> IsConsistentAsync(PackageQueryContext context, PackageConsistencyState state, IProgressReport progressReport);
        Task PopulateStateAsync(PackageQueryContext context, PackageConsistencyState state, IProgressReport progressReport);
    }
}

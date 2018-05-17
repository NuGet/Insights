using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IConsistencyService<T> where T : IConsistencyReport
    {
        Task<T> GetReportAsync(PackageQueryContext context, PackageConsistencyState state, IProgressReporter progressReporter);
        Task<bool> IsConsistentAsync(PackageQueryContext context, PackageConsistencyState state, IProgressReporter progressReporter);
        Task PopulateStateAsync(PackageQueryContext context, PackageConsistencyState state, IProgressReporter progressReporter);
    }
}

using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IConsistencyService<T> where T : IConsistencyReport
    {
        Task<T> GetReportAsync(PackageConsistencyContext context, PackageConsistencyState state, IProgressReporter progressReporter);
        Task<bool> IsConsistentAsync(PackageConsistencyContext context, PackageConsistencyState state, IProgressReporter progressReporter);
        Task PopulateStateAsync(PackageConsistencyContext context, PackageConsistencyState state, IProgressReporter progressReporter);
    }
}

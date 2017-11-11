using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IConsistencyService<T> where T : IConsistencyReport
    {
        Task<T> GetReportAsync(PackageQueryContext context);
        Task<bool> IsConsistentAsync(PackageQueryContext context);
    }
}

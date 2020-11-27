using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Entities
{
    public interface IDatabaseLeaseService
    {
        Task<LeaseEntity> AcquireAsync(string name, TimeSpan leaseDuration);
        Task BreakAsync(string name);
        Task<LeaseEntity> GetOrNullAsync(string name);
        Task ReleaseAsync(LeaseEntity lease);
        Task RenewAsync(LeaseEntity lease, TimeSpan leaseDuration);
        Task<DatabaseLeaseResult> TryAcquireAsync(string name, TimeSpan leaseDuration);
        Task<bool> TryReleaseAsync(LeaseEntity lease);
        Task<bool> TryRenewAsync(LeaseEntity lease, TimeSpan leaseDuration);
    }
}
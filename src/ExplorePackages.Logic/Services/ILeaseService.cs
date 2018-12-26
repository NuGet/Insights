using System;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public interface ILeaseService
    {
        Task<LeaseEntity> GetOrNullAsync(string name);
        Task<LeaseResult> TryAcquireAsync(string name, TimeSpan duration);
        Task<LeaseResult> TryReleaseAsync(LeaseEntity lease);
        Task<LeaseResult> RenewAsync(LeaseEntity lease, TimeSpan duration);
    }
}
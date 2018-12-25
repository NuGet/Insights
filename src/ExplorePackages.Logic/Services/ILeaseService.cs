using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface ILeaseService
    {
        Task<LeaseResult> GetAsync(string name);
        Task<LeaseResult> TryAcquireAsync(string name, TimeSpan duration);
    }
}
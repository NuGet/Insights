using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public interface ILeaseService
    {
        Task<LeaseResult> GetAsync(string name);
        Task<LeaseResult> TryAcquireAsync(string name);
    }
}
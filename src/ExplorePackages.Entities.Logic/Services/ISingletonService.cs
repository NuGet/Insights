using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Entities
{
    public interface ISingletonService
    {
        Task AcquireOrRenewAsync();
        Task BreakAsync();
        Task ReleaseInAsync(TimeSpan duration);
        Task RenewAsync();
    }
}
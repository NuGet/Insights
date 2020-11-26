using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface ISingletonService
    {
        Task AcquireOrRenewAsync();
        Task BreakAsync();
        Task ReleaseInAsync(TimeSpan duration);
        Task RenewAsync();
    }
}
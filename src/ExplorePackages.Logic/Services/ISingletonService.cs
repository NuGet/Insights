using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface ISingletonService
    {
        Task AcquireOrRenewAsync();
        Task ReleaseAsync();
        Task RenewAsync();
    }
}
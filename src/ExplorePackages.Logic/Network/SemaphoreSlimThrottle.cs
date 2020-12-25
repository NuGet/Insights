using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public class SemaphoreSlimThrottle : IThrottle
    {
        private readonly SemaphoreSlim _semaphoreSlim;

        public SemaphoreSlimThrottle(SemaphoreSlim semaphoreSlim)
        {
            _semaphoreSlim = semaphoreSlim;
        }

        public Task WaitAsync() => _semaphoreSlim.WaitAsync();

        public void Release() => _semaphoreSlim.Release();
    }
}

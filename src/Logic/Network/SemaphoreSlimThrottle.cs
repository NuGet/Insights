using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class SemaphoreSlimThrottle : IThrottle
    {
        private readonly SemaphoreSlim _semaphoreSlim;

        public SemaphoreSlimThrottle(SemaphoreSlim semaphoreSlim)
        {
            _semaphoreSlim = semaphoreSlim;
        }

        public Task WaitAsync()
        {
            return _semaphoreSlim.WaitAsync();
        }

        public void Release()
        {
            _semaphoreSlim.Release();
        }
    }
}

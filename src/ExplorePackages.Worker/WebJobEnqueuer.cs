using System;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic.Worker;
using Microsoft.Azure.WebJobs;

namespace Knapcode.ExplorePackages.Worker
{
    public class WebJobEnqueuer : IRawMessageEnqueuer
    {
        private IAsyncCollector<byte[]> _collector;

        public void SetCollector(IAsyncCollector<byte[]> collector)
        {
            var output = Interlocked.CompareExchange(ref _collector, collector, null);
            if (output != null)
            {
                throw new InvalidOperationException("The collector has already been set.");
            }
        }

        public async Task AddAsync(byte[] message)
        {
            if (_collector == null)
            {
                throw new InvalidOperationException("The collector has not been set.");
            }

            await _collector.AddAsync(message);
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic.Worker;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Knapcode.ExplorePackages.Worker
{
    public class UnencodedCloudQueueEnqueuer : IRawMessageEnqueuer
    {
        private CloudQueue _target;

        public void SetTarget(CloudQueue target)
        {
            target.EncodeMessage = false;

            var output = Interlocked.CompareExchange(ref _target, target, null);
            if (output != null)
            {
                throw new InvalidOperationException("The target has already been set.");
            }
        }

        public async Task AddAsync(string message)
        {
            if (_target == null)
            {
                throw new InvalidOperationException("The target has not been set.");
            }

            await _target.AddMessageAsync(new CloudQueueMessage(message));
        }
    }
}

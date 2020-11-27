using System;
using System.Threading;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class ExternalWorkerQueueFactory : IWorkerQueueFactory
    {
        private CloudQueue _target;

        public void SetTarget(CloudQueue target)
        {
            var output = Interlocked.CompareExchange(ref _target, target, null);
            if (output != null)
            {
                throw new InvalidOperationException("The target has already been set.");
            }
        }

        public CloudQueue GetQueue()
        {
            if (_target == null)
            {
                throw new InvalidOperationException("The target has not been set.");
            }

            return _target;
        }
    }
}

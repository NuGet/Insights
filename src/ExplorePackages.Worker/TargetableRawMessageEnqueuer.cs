using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic.Worker;

namespace Knapcode.ExplorePackages.Worker
{
    public class TargetableRawMessageEnqueuer : IRawMessageEnqueuer
    {
        private IRawMessageEnqueuer _target;

        public BulkEnqueueStrategy BulkEnqueueStrategy => _target.BulkEnqueueStrategy;

        public void SetTarget(IRawMessageEnqueuer target)
        {
            var output = Interlocked.CompareExchange(ref _target, target, null);
            if (output != null)
            {
                throw new InvalidOperationException("The target has already been set.");
            }
        }

        public async Task AddAsync(IReadOnlyList<string> messages)
        {
            if (_target == null)
            {
                throw new InvalidOperationException("The target has not been set.");
            }

            await _target.AddAsync(messages);
        }

        public async Task AddAsync(IReadOnlyList<string> messages, TimeSpan notBefore)
        {
            if (_target == null)
            {
                throw new InvalidOperationException("The target has not been set.");
            }

            await _target.AddAsync(messages, notBefore);
        }
    }
}

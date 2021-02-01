using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public class LoopingMessageProcessor<T> : ITaskStateMessageProcessor<T> where T : ILoopingMessage
    {
        private readonly ILoopingMessageProcessor<T> _processor;
        private readonly AutoRenewingStorageLeaseService _leaseService;

        public LoopingMessageProcessor(
            ILoopingMessageProcessor<T> processor,
            AutoRenewingStorageLeaseService leaseService)
        {
            _processor = processor;
            _leaseService = leaseService;
        }

        public async Task<bool> ProcessAsync(T message, int dequeueCount)
        {
            // Only one function -- looping or non-looping should be executing at a time.
            await using (var lease = await _leaseService.TryAcquireAsync(_processor.LeaseName))
            {
                if (message.Loop)
                {
                    return await ProcessLoopingAsync(message, dequeueCount, lease);
                }
                else
                {
                    return await ProcessNonLoopingAsync(message, dequeueCount, lease);
                }
            }
        }

        private async Task<bool> ProcessNonLoopingAsync(T message, int dequeueCount, AutoRenewingStorageLeaseResult lease)
        {
            if (!lease.Acquired)
            {
                // If the message is non-looping and the lease is acquired, ignore this message.
                return true;
            }

            return await _processor.ProcessAsync(message, dequeueCount);
        }

        private async Task<bool> ProcessLoopingAsync(T message, int dequeueCount, AutoRenewingStorageLeaseResult lease)
        {
            if (!lease.Acquired)
            {
                // If the message is looping and the lease is not acquired, ignore this message but schedule the next one.
                await ScheduleLoopAsync();
                return true;
            }

            await using (var loopLease = await _leaseService.TryAcquireAsync(_processor.LeaseName + "-Loop"))
            {
                if (!lease.Acquired)
                {
                    // If there is another loop message already running, ignore this message.
                    return true;
                }

                if (await _processor.ProcessAsync(message, dequeueCount))
                {
                    // If the work is completed successfully, schedule the next one.
                    await ScheduleLoopAsync();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private async Task ScheduleLoopAsync()
        {
            await _processor.StartAsync();
        }
    }
}

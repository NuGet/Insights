using System;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;

namespace Knapcode.ExplorePackages.Worker
{
    public class CloudQueueProcessor
    {
        private readonly IServiceProvider _serviceProvider;

        public CloudQueueProcessor(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task ProcessAsync(
            [QueueTrigger("queue")] byte[] message,
            [Queue("queue")] ICollector<byte[]> collector)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var enqueuer = scope.ServiceProvider.GetRequiredService<WebJobMessageEnqueuer>();
                enqueuer.SetCollector(collector);

                var messageProcessor = scope.ServiceProvider.GetRequiredService<GenericMessageProcessor>();
                await messageProcessor.ProcessAsync(message);
            }
        }
    }
}

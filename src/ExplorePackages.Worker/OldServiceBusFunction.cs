using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Logic.Worker;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs;

namespace Knapcode.ExplorePackages.Worker
{
    public class OldServiceBusFunction
    {
        private const string Connection = ExplorePackagesSettings.DefaultSectionName + ":" + nameof(ExplorePackagesSettings.ServiceBusConnectionString);

        private readonly GenericMessageProcessor _messageProcessor;
        private readonly TargetableRawMessageEnqueuer _enqueuer;
        private readonly OldServiceBusEnqueuer _innerEnqueuer;

        public OldServiceBusFunction(TargetableRawMessageEnqueuer enqueuer, OldServiceBusEnqueuer innerEnqueuer, GenericMessageProcessor messageProcessor)
        {
            _enqueuer = enqueuer;
            _innerEnqueuer = innerEnqueuer;
            _messageProcessor = messageProcessor;
        }

        // [FunctionName("OldServiceBusFunction")]
        public async Task ProcessAsync(
            [ServiceBusTrigger("queue", Connection = Connection)] string message,
            [ServiceBus("queue", Connection = Connection)] MessageSender target)
        {
            _innerEnqueuer.SetTarget(target);
            _enqueuer.SetTarget(_innerEnqueuer);
            await _messageProcessor.ProcessAsync(message);
        }
    }
}

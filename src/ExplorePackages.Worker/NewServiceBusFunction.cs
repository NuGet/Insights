using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Logic.Worker;
using Microsoft.Azure.WebJobs;

namespace Knapcode.ExplorePackages.Worker
{
    public class NewServiceBusFunction
    {
        private const string Connection = ExplorePackagesSettings.DefaultSectionName + ":" + nameof(ExplorePackagesSettings.ServiceBusConnectionString);

        private readonly GenericMessageProcessor _messageProcessor;
        private readonly TargetableRawMessageEnqueuer _enqueuer;
        private readonly NewServiceBusEnqueuer _innerEnqueuer;

        public NewServiceBusFunction(TargetableRawMessageEnqueuer enqueuer, NewServiceBusEnqueuer innerEnqueuer, GenericMessageProcessor messageProcessor)
        {
            _enqueuer = enqueuer;
            _innerEnqueuer = innerEnqueuer;
            _messageProcessor = messageProcessor;
        }

        // [FunctionName("NewServiceBusFunction")]
        public async Task ProcessAsync(
            [ServiceBusTrigger("queue", Connection = Connection)] string message)
        {
            _enqueuer.SetTarget(_innerEnqueuer);
            await _messageProcessor.ProcessAsync(message);
        }
    }
}

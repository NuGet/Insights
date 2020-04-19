using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Logic.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Queues;

namespace Knapcode.ExplorePackages.Worker
{
    public class QueueFunction
    {
        private const string Connection = ExplorePackagesSettings.DefaultSectionName + ":" + nameof(ExplorePackagesSettings.StorageConnectionString);
        private readonly WebJobEnqueuer _enqueuer;
        private readonly GenericMessageProcessor _messageProcessor;

        public QueueFunction(WebJobEnqueuer webJobEnqueuer, GenericMessageProcessor messageProcessor)
        {
            _enqueuer = webJobEnqueuer;
            _messageProcessor = messageProcessor;
        }

        [FunctionName("QueueFunction")]
        public async Task ProcessAsync(
            [QueueTrigger("test", Connection = Connection)] byte[] message,
            [Queue("test")] IAsyncCollector<byte[]> collector)
        {
            _enqueuer.SetCollector(collector);
            await _messageProcessor.ProcessAsync(message);
        }
    }
}

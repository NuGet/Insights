using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Logic.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Knapcode.ExplorePackages.Worker
{
    public class QueueFunction
    {
        private const string Connection = ExplorePackagesSettings.DefaultSectionName + ":" + nameof(ExplorePackagesSettings.StorageConnectionString);
        private readonly UnencodedCloudQueueEnqueuer _enqueuer;
        private readonly GenericMessageProcessor _messageProcessor;

        public QueueFunction(UnencodedCloudQueueEnqueuer enqueuer, GenericMessageProcessor messageProcessor)
        {
            _enqueuer = enqueuer;
            _messageProcessor = messageProcessor;
        }

        [FunctionName("QueueFunction")]
        public async Task ProcessAsync(
            [QueueTrigger("queue", Connection = Connection)] string message,
            [Queue("queue")] CloudQueue queue)
        {
            _enqueuer.SetTarget(queue);
            await _messageProcessor.ProcessAsync(message);
        }
    }
}

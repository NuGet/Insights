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

        private readonly GenericMessageProcessor _messageProcessor;
        private readonly TargetableRawMessageEnqueuer _rawMessageEnqueuer;
        private readonly ExternalWorkerQueueFactory _workerQueueFactory;
        private readonly QueueStorageEnqueuer _queueStorageEnqueuer;

        public QueueFunction(
            ExternalWorkerQueueFactory workerQueueFactory,
            QueueStorageEnqueuer queueStorageEnqueuer,
            TargetableRawMessageEnqueuer rawMessageEnqueuer,
            GenericMessageProcessor messageProcessor)
        {
            _workerQueueFactory = workerQueueFactory;
            _queueStorageEnqueuer = queueStorageEnqueuer;
            _rawMessageEnqueuer = rawMessageEnqueuer;
            _messageProcessor = messageProcessor;
        }

        [FunctionName("QueueFunction")]
        public async Task ProcessAsync(
            [QueueTrigger("test", Connection = Connection)] string message,
            [Queue("test", Connection = Connection)] CloudQueue target)
        {
            target.EncodeMessage = false;
            _workerQueueFactory.SetTarget(target);
            _rawMessageEnqueuer.SetTarget(_queueStorageEnqueuer);
            await _messageProcessor.ProcessAsync(message);
        }
    }
}

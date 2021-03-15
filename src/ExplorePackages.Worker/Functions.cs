using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Knapcode.ExplorePackages.Worker
{
    public class Functions
    {
        private const string WorkerQueue = ExplorePackagesSettings.DefaultSectionName + ":" + nameof(ExplorePackagesWorkerSettings.WorkerQueueName);
        private const string WorkerQueueVariable = "%" + WorkerQueue + "%";
        private const string StorageConnection = ExplorePackagesSettings.DefaultSectionName + ":" + nameof(ExplorePackagesSettings.StorageConnectionString);

        private readonly TempStreamLeaseScope _tempStreamLeaseScope;
        private readonly TimerExecutionService _timerExecutionService;
        private readonly IGenericMessageProcessor _messageProcessor;

        public Functions(
            TempStreamLeaseScope tempStreamLeaseScope,
            TimerExecutionService timerExecutionService,
            IGenericMessageProcessor messageProcessor)
        {
            _tempStreamLeaseScope = tempStreamLeaseScope;
            _timerExecutionService = timerExecutionService;
            _messageProcessor = messageProcessor;
        }

        [FunctionName("TimerFunction")]
        public async Task TimerAsync(
            [TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo)
        {
            await using var scopeOwnership = _tempStreamLeaseScope.TakeOwnership();
            await _timerExecutionService.ExecuteAsync(isEnabledDefault: true);
        }

        [FunctionName("WorkerQueueFunction")]
        public async Task WorkerQueueAsync(
            [QueueTrigger(WorkerQueueVariable, Connection = StorageConnection)] CloudQueueMessage message)
        {
            await using var scopeOwnership = _tempStreamLeaseScope.TakeOwnership();
            await _messageProcessor.ProcessSingleAsync(message.AsString, message.DequeueCount);
        }
    }
}

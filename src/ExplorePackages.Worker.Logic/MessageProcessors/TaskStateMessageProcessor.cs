using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class TaskStateMessageProcessor<T> : IMessageProcessor<T> where T : ITaskStateMessage
    {
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly ITaskStateMessageProcessor<T> _processor;
        private readonly ILogger<TaskStateMessageProcessor<T>> _logger;

        public TaskStateMessageProcessor(
            TaskStateStorageService taskStateStorageService,
            MessageEnqueuer messageEnqueuer,
            ITaskStateMessageProcessor<T> processor,
            ILogger<TaskStateMessageProcessor<T>> logger)
        {
            _taskStateStorageService = taskStateStorageService;
            _messageEnqueuer = messageEnqueuer;
            _processor = processor;
            _logger = logger;
        }

        public async Task ProcessAsync(T message, int dequeueCount)
        {
            var taskState = await _taskStateStorageService.GetAsync(message.TaskStateKey);
            if (taskState == null)
            {
                message.AttemptCount++;
                if (message.AttemptCount <= 10)
                {
                    _logger.LogWarning(
                        "Attempt {AttemptCount}: no task state for {StorageSuffix}, {PartitionKey}, {RowKey} was found. Trying again.",
                        message.TaskStateKey.StorageSuffix,
                        message.TaskStateKey.PartitionKey,
                        message.TaskStateKey.RowKey);
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                }
                else
                {
                    _logger.LogError("Attempt {AttemptCount}: no task state for {StorageSuffix}, {PartitionKey}, {RowKey} was found. Giving up.", message.AttemptCount);
                }

                return;
            }

            if (await _processor.ProcessAsync(message, dequeueCount))
            {
                await _taskStateStorageService.DeleteAsync(taskState);
            }
            else
            {
                message.AttemptCount++;
                await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
            }
        }
    }
}

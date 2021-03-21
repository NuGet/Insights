using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class CsvCompactorProcessor<T> : IMessageProcessor<CsvCompactMessage<T>> where T : ICsvRecord<T>, new()
    {
        private readonly AppendResultStorageService _storageService;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly ICsvCompactor<T> _compactor;
        private readonly ILogger<CsvCompactorProcessor<T>> _logger;

        public CsvCompactorProcessor(
            AppendResultStorageService storageService,
            TaskStateStorageService taskStateStorageService,
            ICsvCompactor<T> compactor,
            ILogger<CsvCompactorProcessor<T>> logger)
        {
            _storageService = storageService;
            _taskStateStorageService = taskStateStorageService;
            _compactor = compactor;
            _logger = logger;
        }

        public async Task ProcessAsync(CsvCompactMessage<T> message, long dequeueCount)
        {
            TaskState taskState;
            if (message.Force && message.TaskStateKey == null)
            {
                taskState = null;
            }
            else
            {
                taskState = await _taskStateStorageService.GetAsync(message.TaskStateKey);
            }

            if (!message.Force && taskState == null)
            {
                _logger.LogWarning("No matching task state was found.");
                return;
            }

            await _storageService.CompactAsync<T>(
                message.SourceContainer,
                _compactor.ResultsContainerName,
                message.Bucket,
                force: message.Force,
                _compactor.Prune);

            if (taskState != null)
            {
                await _taskStateStorageService.DeleteAsync(taskState);
            }
        }
    }
}

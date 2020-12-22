using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Knapcode.ExplorePackages.Worker
{
    public class WorkerQueueFunction
    {
        private const string WorkerQueue = ExplorePackagesSettings.DefaultSectionName + ":" + nameof(ExplorePackagesWorkerSettings.WorkerQueueName);
        private const string WorkerQueueVariable = "%" + WorkerQueue + "%";
        private const string StorageConnection = ExplorePackagesSettings.DefaultSectionName + ":" + nameof(ExplorePackagesSettings.StorageConnectionString);

        private readonly GenericMessageProcessor _messageProcessor;
        private readonly ILogger<WorkerQueueFunction> _logger;

        public WorkerQueueFunction(
            GenericMessageProcessor messageProcessor,
            ILogger<WorkerQueueFunction> logger)
        {
            _messageProcessor = messageProcessor;
            _logger = logger;
        }

        [FunctionName("WorkerQueueFunction")]
        public async Task ProcessAsync(
            [QueueTrigger(WorkerQueueVariable, Connection = StorageConnection)] CloudQueueMessage message)
        {
            try
            {
                await _messageProcessor.ProcessAsync(message.AsString, message.DequeueCount);
            }
            catch (Exception ex)
            {
                _logger.LogError("An exception occurred." + Environment.NewLine + "{ExceptionString}", ex.ToString());
                throw;
            }
        }
    }
}

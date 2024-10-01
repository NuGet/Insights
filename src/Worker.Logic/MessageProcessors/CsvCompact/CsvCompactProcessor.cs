// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class CsvCompactProcessor<T> : ITaskStateMessageProcessor<CsvCompactMessage<T>> where T : IAggregatedCsvRecord<T>
    {
        private readonly AppendResultStorageService _storageService;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly ICsvResultStorage<T> _storage;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<CsvCompactProcessor<T>> _logger;

        public CsvCompactProcessor(
            AppendResultStorageService storageService,
            TaskStateStorageService taskStateStorageService,
            ICsvResultStorage<T> storage,
            IMessageEnqueuer messageEnqueuer,
            ILogger<CsvCompactProcessor<T>> logger)
        {
            _storageService = storageService;
            _taskStateStorageService = taskStateStorageService;
            _storage = storage;
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task<TaskStateProcessResult> ProcessAsync(CsvCompactMessage<T> message, TaskState taskState, long dequeueCount)
        {
            using var loggerScope = _logger.BeginScope("CSV compact: {Scope_CsvCompactContainer} {Scope_CsvCompactBucket}", _storage.ResultContainerName, message.Bucket);

            await _storageService.CompactAsync<T>(
                message.SourceTable,
                _storage.ResultContainerName,
                message.Bucket);

            return TaskStateProcessResult.Complete;
        }
    }
}

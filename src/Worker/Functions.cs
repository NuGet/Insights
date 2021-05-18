// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Queue;
using static NuGet.Insights.Worker.CustomNameResolver;
using static NuGet.Insights.Worker.CustomStorageAccountProvider;

namespace NuGet.Insights.Worker
{
    public class Functions
    {
        private static bool _isInitialized = false;
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
            [TimerTrigger("0 * * * * *")] TimerInfo timerInfo)
        {
            await using var scopeOwnership = _tempStreamLeaseScope.TakeOwnership();
            if (!_isInitialized)
            {
                await _timerExecutionService.InitializeAsync();
                _isInitialized = true;
            }
            await _timerExecutionService.ExecuteAsync();
        }

        [FunctionName("WorkQueueFunction")]
        public async Task WorkQueueAsync(
            [QueueTrigger(WorkQueueVariable, Connection = ConnectionName)] CloudQueueMessage message)
        {
            await ProcessMessageAsync(QueueType.Work, message);
        }

        [FunctionName("ExpandQueueFunction")]
        public async Task ExpandQueueAsync(
            [QueueTrigger(ExpandQueueVariable, Connection = ConnectionName)] CloudQueueMessage message)
        {
            await ProcessMessageAsync(QueueType.Expand, message);
        }

        private async Task ProcessMessageAsync(QueueType queue, CloudQueueMessage message)
        {
            await using var scopeOwnership = _tempStreamLeaseScope.TakeOwnership();
            await _messageProcessor.ProcessSingleAsync(queue, message.AsString, message.DequeueCount);
        }
    }
}

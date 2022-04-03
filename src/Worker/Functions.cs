// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.WebJobs;
using static NuGet.Insights.Worker.CustomNameResolver;

namespace NuGet.Insights.Worker
{
    public class Functions
    {
        private const string ConnectionName = "QueueTriggerConnection";
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
            [QueueTrigger(WorkQueueVariable, Connection = ConnectionName)] QueueMessage message)
        {
            await ProcessMessageAsync(QueueType.Work, message);
        }

        [FunctionName("ExpandQueueFunction")]
        public async Task ExpandQueueAsync(
            [QueueTrigger(ExpandQueueVariable, Connection = ConnectionName)] QueueMessage message)
        {
            await ProcessMessageAsync(QueueType.Expand, message);
        }

        private async Task ProcessMessageAsync(QueueType queue, QueueMessage message)
        {
            await using var scopeOwnership = _tempStreamLeaseScope.TakeOwnership();
            await _messageProcessor.ProcessSingleAsync(queue, message.Body.ToMemory(), message.DequeueCount);
        }
    }
}

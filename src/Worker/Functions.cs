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
        private static bool _isMetricsFunctionInitialized = false;
        private static bool _isTimerFunctionInitialized = false;
        private readonly MetricsTimer _metricsTimer;
        private readonly TempStreamLeaseScope _tempStreamLeaseScope;
        private readonly TimerExecutionService _timerExecutionService;
        private readonly IGenericMessageProcessor _messageProcessor;

        public Functions(
            MetricsTimer metricsTimer,
            TempStreamLeaseScope tempStreamLeaseScope,
            TimerExecutionService timerExecutionService,
            IGenericMessageProcessor messageProcessor)
        {
            _metricsTimer = metricsTimer;
            _tempStreamLeaseScope = tempStreamLeaseScope;
            _timerExecutionService = timerExecutionService;
            _messageProcessor = messageProcessor;
        }

        [FunctionName("MetricsFunction")]
        public async Task MetricsFunction(
            [TimerTrigger("*/10 * * * * *")] TimerInfo timerInfo)
        {
            if (!_isMetricsFunctionInitialized)
            {
                await _metricsTimer.InitializeAsync();
                _isMetricsFunctionInitialized = true;
            }
            await _metricsTimer.InitializeAsync();
        }

        [FunctionName("TimerFunction")]
        public async Task TimerAsync(
            [TimerTrigger("0 * * * * *")] TimerInfo timerInfo)
        {
            await using var scopeOwnership = _tempStreamLeaseScope.TakeOwnership();
            if (!_isTimerFunctionInitialized)
            {
                await _timerExecutionService.InitializeAsync();
                _isTimerFunctionInitialized = true;
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

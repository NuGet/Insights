// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace NuGet.Insights.Worker
{
    public class Functions
    {
        private const string WorkQueueVariable = $"%{NuGetInsightsSettings.DefaultSectionName}:{nameof(NuGetInsightsWorkerSettings.WorkQueueName)}%";
        private const string ExpandQueueVariable = $"%{NuGetInsightsSettings.DefaultSectionName}:{nameof(NuGetInsightsWorkerSettings.ExpandQueueName)}%";
        private const string ConnectionName = "QueueTriggerConnection";

        private readonly MetricsTimer _metricsTimer;
        private readonly TimerExecutionService _timerExecutionService;
        private readonly IGenericMessageProcessor _messageProcessor;

        public Functions(
            MetricsTimer metricsTimer,
            TimerExecutionService timerExecutionService,
            IGenericMessageProcessor messageProcessor)
        {
            _metricsTimer = metricsTimer;
            _timerExecutionService = timerExecutionService;
            _messageProcessor = messageProcessor;
        }

        [Function("HealthFunction")]
        public HttpResponseData HealthFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "GET", Route = "healthz")] HttpRequestData request)
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.WriteString("The worker is alive.");
            return response;
        }

        [Function("MetricsFunction")]
        public async Task MetricsFunction(
            [TimerTrigger("*/10 * * * * *")] TimerInfo timerInfo)
        {
            await _metricsTimer.InitializeAsync();
            await _metricsTimer.ExecuteAsync();
        }

        [Function("TimerFunction")]
        public async Task TimerAsync(
            [TimerTrigger("0 * * * * *")] TimerInfo timerInfo)
        {
            await _timerExecutionService.InitializeAsync();
            await _timerExecutionService.ExecuteAsync();
        }

        [Function("WorkQueueFunction")]
        public async Task WorkQueueAsync(
            [QueueTrigger(WorkQueueVariable, Connection = ConnectionName)] QueueMessage message)
        {
            await ProcessMessageAsync(QueueType.Work, message);
        }

        [Function("ExpandQueueFunction")]
        public async Task ExpandQueueAsync(
            [QueueTrigger(ExpandQueueVariable, Connection = ConnectionName)] QueueMessage message)
        {
            await ProcessMessageAsync(QueueType.Expand, message);
        }

        private async Task ProcessMessageAsync(QueueType queue, QueueMessage message)
        {
            await _messageProcessor.ProcessSingleAsync(queue, message.Body.ToMemory(), message.DequeueCount);
        }
    }
}

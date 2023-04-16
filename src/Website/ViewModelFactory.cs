// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Insights.Website.Models;
using NuGet.Insights.Worker;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Website
{
    public class ViewModelFactory
    {
        private readonly CatalogCommitTimestampProvider _catalogCommitTimestampProvider;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly CatalogScanCursorService _catalogScanCursorService;
        private readonly CatalogScanService _catalogScanService;
        private readonly IRemoteCursorClient _remoteCursorClient;
        private readonly TimerExecutionService _timerExecutionService;
        private readonly WorkflowService _workflowService;
        private readonly MoveMessagesTaskQueue _moveMessagesTaskQueue;

        public ViewModelFactory(
            CatalogCommitTimestampProvider catalogCommitTimestampProvider,
            IRawMessageEnqueuer rawMessageEnqueuer,
            CatalogScanStorageService catalogScanStorageService,
            CatalogScanCursorService catalogScanCursorService,
            CatalogScanService catalogScanService,
            IRemoteCursorClient remoteCursorClient,
            TimerExecutionService timerExecutionService,
            WorkflowService workflowService,
            MoveMessagesTaskQueue moveMessagesTaskQueue)
        {
            _catalogCommitTimestampProvider = catalogCommitTimestampProvider;
            _rawMessageEnqueuer = rawMessageEnqueuer;
            _catalogScanStorageService = catalogScanStorageService;
            _catalogScanCursorService = catalogScanCursorService;
            _catalogScanService = catalogScanService;
            _remoteCursorClient = remoteCursorClient;
            _timerExecutionService = timerExecutionService;
            _workflowService = workflowService;
            _moveMessagesTaskQueue = moveMessagesTaskQueue;
        }

        public async Task<AdminViewModel> GetAdminViewModelAsync()
        {
            var workQueueTask = GetQueueAsync(QueueType.Work);
            var expandQueueTask = GetQueueAsync(QueueType.Expand);

            var catalogScanTasks = _catalogScanCursorService
                .StartableDriverTypes
                .Select(GetCatalogScanAsync)
                .ToList();

            var isWorkflowRunningTask = _workflowService.IsWorkflowRunningAsync();
            var timerStatesTask = _timerExecutionService.GetStateAsync();
            var catalogCommitTimestampTask = _remoteCursorClient.GetCatalogAsync();

            await Task.WhenAll(
                workQueueTask,
                expandQueueTask,
                isWorkflowRunningTask,
                timerStatesTask,
                catalogCommitTimestampTask);

            var catalogScans = await Task.WhenAll(catalogScanTasks);

            // Calculate the cursor age.
            var catalogCommitTimestamp = await catalogCommitTimestampTask;
            foreach (var catalogScan in catalogScans)
            {
                var min = catalogScan.Cursor.Value;
                if (min < CatalogClient.NuGetOrgMin)
                {
                    min = CatalogClient.NuGetOrgMin;
                }

                catalogScan.CursorAge = catalogCommitTimestamp - min;
            }

            // Calculate the next default max, which supports processing the catalog one commit at a time.
            var catalogScanMin = catalogScans.Where(x => x.IsEnabled).Min(x => x.Cursor.Value);
            var nextCommitTimestamp = await _catalogCommitTimestampProvider.GetNextAsync(catalogScanMin);

            var model = new AdminViewModel
            {
                DefaultMax = nextCommitTimestamp ?? catalogScanMin,
                IsWorkflowRunning = await isWorkflowRunningTask,
                WorkQueue = await workQueueTask,
                ExpandQueue = await expandQueueTask,
                CatalogScans = catalogScans,
                TimerStates = await timerStatesTask,
            };

            return model;
        }

        private async Task<QueueViewModel> GetQueueAsync(QueueType queue)
        {
            var approximateMessageCountTask = _rawMessageEnqueuer.GetApproximateMessageCountAsync(queue);
            var poisonApproximateMessageCountTask = _rawMessageEnqueuer.GetPoisonApproximateMessageCountAsync(queue);
            var availableMessageCountLowerBoundTask = _rawMessageEnqueuer.GetAvailableMessageCountLowerBoundAsync(queue, StorageUtility.MaxDequeueCount);
            var poisonAvailableMessageCountLowerBoundTask = _rawMessageEnqueuer.GetPoisonAvailableMessageCountLowerBoundAsync(queue, StorageUtility.MaxDequeueCount);

            var model = new QueueViewModel
            {
                QueueType = queue,
                ApproximateMessageCount = await approximateMessageCountTask,
                PoisonApproximateMessageCount = await poisonApproximateMessageCountTask,
                AvailableMessageCountLowerBound = await availableMessageCountLowerBoundTask,
                PoisonAvailableMessageCountLowerBound = await poisonAvailableMessageCountLowerBoundTask,
                MoveMainToPoisonState = GetMoveQueueMessagesState(queue, fromPoison: false),
                MovePoisonToMainState = GetMoveQueueMessagesState(queue, fromPoison: true),
            };

            model.AvailableMessageCountIsExact = model.AvailableMessageCountLowerBound < StorageUtility.MaxDequeueCount;
            model.PoisonAvailableMessageCountIsExact = model.PoisonAvailableMessageCountLowerBound < StorageUtility.MaxDequeueCount;

            return model;
        }

        private MoveQueueMessagesState GetMoveQueueMessagesState(QueueType queueType, bool fromPoison)
        {
            var task = new MoveMessagesTask(queueType, fromPoison, queueType, !fromPoison);
            if (_moveMessagesTaskQueue.IsScheduled(task))
            {
                return MoveQueueMessagesState.Scheduled;
            }
            else if (_moveMessagesTaskQueue.IsWorking(task))
            {
                return MoveQueueMessagesState.Working;
            }
            else
            {
                return MoveQueueMessagesState.None;
            }
        }

        private async Task<CatalogScanViewModel> GetCatalogScanAsync(CatalogScanDriverType driverType)
        {
            var cursor = await _catalogScanCursorService.GetCursorAsync(driverType);
            var latestScans = await _catalogScanStorageService.GetLatestIndexScansAsync(cursor.GetName(), maxEntities: 5);
            var nextCommitTimestamp = await _catalogCommitTimestampProvider.GetNextAsync(cursor.Value);

            return new CatalogScanViewModel
            {
                DriverType = driverType,
                Cursor = cursor,
                LatestScans = latestScans,
                SupportsReprocess = _catalogScanService.SupportsReprocess(driverType),
                OnlyLatestLeavesSupport = _catalogScanService.GetOnlyLatestLeavesSupport(driverType),
                IsEnabled = _catalogScanService.IsEnabled(driverType),
                DefaultMax = nextCommitTimestamp ?? DateTimeOffset.Parse("2015-02-01T06:22:45.8488496Z"),
            };
        }
    }
}

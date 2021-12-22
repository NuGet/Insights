// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.KustoIngestion;

namespace NuGet.Insights.Worker.Workflow
{
    public class WorkflowService
    {
        private readonly WorkflowStorageService _workflowStorageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly IReadOnlyList<IAuxiliaryFileUpdaterService> _auxiliaryFileUpdaterServices;
        private readonly KustoIngestionService _kustoIngestionService;
        private readonly KustoIngestionStorageService _kustoIngestionStorageService;
        private readonly IMessageEnqueuer _messageEnqueuer;

        public WorkflowService(
            WorkflowStorageService workflowStorageService,
            AutoRenewingStorageLeaseService leaseService,
            CatalogScanStorageService catalogScanStorageService,
            IEnumerable<IAuxiliaryFileUpdaterService> auxiliaryFileUpdaterServices,
            KustoIngestionService kustoIngestionService,
            KustoIngestionStorageService kustoIngestionStorageService,
            IMessageEnqueuer messageEnqueuer)
        {
            _workflowStorageService = workflowStorageService;
            _leaseService = leaseService;
            _catalogScanStorageService = catalogScanStorageService;
            _auxiliaryFileUpdaterServices = auxiliaryFileUpdaterServices.ToList();
            _kustoIngestionService = kustoIngestionService;
            _kustoIngestionStorageService = kustoIngestionStorageService;
            _messageEnqueuer = messageEnqueuer;
        }

        public async Task InitializeAsync()
        {
            await _workflowStorageService.InitializeAsync();
            await _leaseService.InitializeAsync();
            await _catalogScanStorageService.InitializeAsync();
            foreach (var service in _auxiliaryFileUpdaterServices)
            {
                await service.InitializeAsync();
            }
            await _kustoIngestionService.InitializeAsync();
            await _messageEnqueuer.InitializeAsync();
        }

        public bool HasRequiredConfiguration => _auxiliaryFileUpdaterServices.All(x => x.HasRequiredConfiguration)
            && _kustoIngestionService.HasRequiredConfiguration;

        public async Task<WorkflowRun> StartAsync(DateTimeOffset? maxCommitTimestamp)
        {
            await using var lease = await _leaseService.TryAcquireAsync("Start-Workflow");
            if (!lease.Acquired)
            {
                return null;
            }

            if (await IsAnyWorkflowStepRunningAsync())
            {
                return null;
            }

            var run = new WorkflowRun(StorageUtility.GenerateDescendingId().ToString())
            {
                Created = DateTimeOffset.UtcNow,
                MaxCommitTimestamp = maxCommitTimestamp,
            };
            await _messageEnqueuer.EnqueueAsync(new[]
            {
                new WorkflowRunMessage
                {
                    WorkflowRunId = run.GetRunId(),
                }
            });
            await _workflowStorageService.AddRunAsync(run);
            return run;
        }

        public async Task<bool> IsWorkflowRunningAsync()
        {
            var runs = await _workflowStorageService.GetRunsAsync();
            return runs.Any(x => x.State != WorkflowRunState.Complete);
        }

        public async Task<bool> IsAnyWorkflowStepRunningAsync()
        {
            return await IsWorkflowRunningAsync()
                || await AreCatalogScansRunningAsync()
                || await AreAuxiliaryFilesRunningAsync()
                || await IsKustoIngestionRunningAsync();
        }

        internal async Task StartAuxiliaryFilesAsync()
        {
            foreach (var service in _auxiliaryFileUpdaterServices)
            {
                await service.StartAsync();
            }
        }

        internal async Task<bool> AreCatalogScansRunningAsync()
        {
            var catalogScans = await _catalogScanStorageService.GetIndexScansAsync();
            return catalogScans.Any(x => x.State != CatalogIndexScanState.Complete);
        }

        internal async Task<bool> AreAuxiliaryFilesRunningAsync()
        {
            foreach (var service in _auxiliaryFileUpdaterServices)
            {
                if (await service.IsRunningAsync())
                {
                    return true;
                }
            }

            return false;
        }

        internal async Task<bool> IsKustoIngestionRunningAsync()
        {
            var kustoIngestions = await _kustoIngestionStorageService.GetIngestionsAsync();
            return kustoIngestions.Any(x => x.State != KustoIngestionState.Complete);
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.KustoIngestion;
using Knapcode.ExplorePackages.Worker.StreamWriterUpdater;

namespace Knapcode.ExplorePackages.Worker.Workflow
{
    public class WorkflowService
    {
        private readonly WorkflowStorageService _workflowStorageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly IStreamWriterUpdaterService<PackageOwnerSet> _ownersService;
        private readonly IStreamWriterUpdaterService<PackageDownloadSet> _downloadsService;
        private readonly KustoIngestionService _kustoIngestionService;
        private readonly KustoIngestionStorageService _kustoIngestionStorageService;
        private readonly IMessageEnqueuer _messageEnqueuer;

        public WorkflowService(
            WorkflowStorageService workflowStorageService,
            AutoRenewingStorageLeaseService leaseService,
            CatalogScanStorageService catalogScanStorageService,
            IStreamWriterUpdaterService<PackageOwnerSet> ownersService,
            IStreamWriterUpdaterService<PackageDownloadSet> downloadsService,
            KustoIngestionService kustoIngestionService,
            KustoIngestionStorageService kustoIngestionStorageService,
            IMessageEnqueuer messageEnqueuer)
        {
            _workflowStorageService = workflowStorageService;
            _leaseService = leaseService;
            _catalogScanStorageService = catalogScanStorageService;
            _ownersService = ownersService;
            _downloadsService = downloadsService;
            _kustoIngestionService = kustoIngestionService;
            _kustoIngestionStorageService = kustoIngestionStorageService;
            _messageEnqueuer = messageEnqueuer;
        }

        public async Task InitializeAsync()
        {
            await _workflowStorageService.InitializeAsync();
            await _leaseService.InitializeAsync();
            await _catalogScanStorageService.InitializeAsync();
            await _ownersService.InitializeAsync();
            await _downloadsService.InitializeAsync();
            await _kustoIngestionService.InitializeAsync();
            await _messageEnqueuer.InitializeAsync();
        }

        public bool HasRequiredConfiguration => _ownersService.HasRequiredConfiguration
            && _downloadsService.HasRequiredConfiguration
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
            await _ownersService.StartAsync();
            await _downloadsService.StartAsync();
        }

        internal async Task<bool> AreCatalogScansRunningAsync()
        {
            var catalogScans = await _catalogScanStorageService.GetIndexScansAsync();
            return catalogScans.Any(x => x.State != CatalogIndexScanState.Complete);
        }

        internal async Task<bool> AreAuxiliaryFilesRunningAsync()
        {
            return await _ownersService.IsRunningAsync()
                || await _downloadsService.IsRunningAsync();
        }

        internal async Task<bool> IsKustoIngestionRunningAsync()
        {
            var kustoIngestions = await _kustoIngestionStorageService.GetIngestionsAsync();
            return kustoIngestions.Any(x => x.State != KustoIngestionState.Complete);
        }
    }
}

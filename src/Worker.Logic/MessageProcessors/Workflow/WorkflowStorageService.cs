// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Data.Tables;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker.Workflow
{
    public class WorkflowStorageService
    {
        private readonly ContainerInitializationState _initializationState;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<WorkflowStorageService> _logger;

        public WorkflowStorageService(
            ServiceClientFactory serviceClientFactory,
            IOptions<NuGetInsightsWorkerSettings> options,
            ITelemetryClient telemetryClient,
            ILogger<WorkflowStorageService> logger)
        {
            _initializationState = ContainerInitializationState.Table(serviceClientFactory, options.Value, options.Value.WorkflowRunTableName);
            _serviceClientFactory = serviceClientFactory;
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _initializationState.InitializeAsync();
        }

        public async Task<WorkflowRun> GetRunAsync(string runId)
        {
            var table = await GetTableAsync();
            return await table.GetEntityOrNullAsync<WorkflowRun>(WorkflowRun.DefaultPartitionKey, runId);
        }

        public async Task AddRunAsync(WorkflowRun run)
        {
            var table = await GetTableAsync();
            var response = await table.AddEntityAsync(run);
            run.UpdateETag(response);
        }

        public async Task ReplaceRunAsync(WorkflowRun run)
        {
            _logger.LogInformation(
                "Updating workflow run {WorkflowRunId} with state {State}.",
                run.RunId,
                run.State);

            var table = await GetTableAsync();
            var response = await table.UpdateEntityAsync(run, run.ETag, TableUpdateMode.Replace);
            run.UpdateETag(response);
        }

        public async Task<IReadOnlyList<WorkflowRun>> GetRunsAsync()
        {
            var table = await GetTableAsync();
            return await table
                .QueryAsync<WorkflowRun>(x => x.PartitionKey == WorkflowRun.DefaultPartitionKey)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<WorkflowRun>> GetLatestRunsAsync(int maxEntities)
        {
            return await (await GetTableAsync())
                .QueryAsync<WorkflowRun>(x => x.PartitionKey == WorkflowRun.DefaultPartitionKey)
                .Take(maxEntities)
                .ToListAsync();
        }

        public async Task DeleteOldRunsAsync(string currentRunId)
        {
            var table = await GetTableAsync();
            var oldRuns = await table
                .QueryAsync<WorkflowRun>(x => x.PartitionKey == WorkflowRun.DefaultPartitionKey
                                              && string.Compare(x.RowKey, currentRunId, StringComparison.Ordinal) > 0)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());

            var oldRunsToDelete = oldRuns
                .OrderByDescending(x => x.Created)
                .Skip(_options.Value.OldWorkflowRunsToKeep)
                .OrderBy(x => x.Created)
                .Where(x => x.State.IsTerminal())
                .ToList();
            _logger.LogInformation("Deleting {Count} old workflow runs.", oldRunsToDelete.Count);

            var batch = new MutableTableTransactionalBatch(table);
            foreach (var scan in oldRunsToDelete)
            {
                if (batch.Count >= StorageUtility.MaxBatchSize)
                {
                    await batch.SubmitBatchAsync();
                    batch = new MutableTableTransactionalBatch(table);
                }

                batch.DeleteEntity(scan.PartitionKey, scan.RowKey, scan.ETag);
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        private async Task<TableClientWithRetryContext> GetTableAsync()
        {
            var serviceClient = await _serviceClientFactory.GetTableServiceClientAsync(_options.Value);
            var table = serviceClient.GetTableClient(_options.Value.WorkflowRunTableName);
            return table;
        }
    }
}

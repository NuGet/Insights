using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.Workflow
{
    public class WorkflowStorageService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<WorkflowStorageService> _logger;

        public WorkflowStorageService(
            ServiceClientFactory serviceClientFactory,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<WorkflowStorageService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await (await GetTableAsync()).CreateIfNotExistsAsync(retry: true);
        }

        public async Task<WorkflowRun> GetRunAsync(string workflowRunId)
        {
            var table = await GetTableAsync();
            return await table.GetEntityOrNullAsync<WorkflowRun>(WorkflowRun.DefaultPartitionKey, workflowRunId);
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
                run.GetRunId(),
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

        private async Task<TableClient> GetTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.WorkflowRunTableName);
        }
    }
}

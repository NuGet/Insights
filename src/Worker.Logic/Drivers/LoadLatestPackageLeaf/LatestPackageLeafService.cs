// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker.LoadLatestPackageLeaf
{
    public class LatestPackageLeafService
    {
        private readonly ContainerInitializationState _initializationState;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public LatestPackageLeafService(
            ServiceClientFactory serviceClientFactory,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _initializationState = ContainerInitializationState.Table(serviceClientFactory, options.Value, options.Value.LatestPackageLeafTableName);
            _serviceClientFactory = serviceClientFactory;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await _initializationState.InitializeAsync();
        }

        public async Task DestroyAsync()
        {
            await _initializationState.DestroyAsync();
        }

        public async Task<LatestPackageLeaf> GetOrNullAsync(string id, string version)
        {
            var table = await GetTableAsync();
            return await table.GetEntityOrNullAsync<LatestPackageLeaf>(
                LatestPackageLeaf.GetPartitionKey(id),
                LatestPackageLeaf.GetRowKey(version));
        }

        internal async Task<TableClientWithRetryContext> GetTableAsync()
        {
            var tableServiceClient = await _serviceClientFactory.GetTableServiceClientAsync(_options.Value);
            var tableClient = tableServiceClient.GetTableClient(_options.Value.LatestPackageLeafTableName);
            return tableClient;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker.LoadLatestPackageLeaf
{
    public class LatestPackageLeafService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public LatestPackageLeafService(
            ServiceClientFactory serviceClientFactory,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await (await GetTableAsync()).CreateIfNotExistsAsync(retry: true);
        }

        public async Task DestroyAsync()
        {
            await (await GetTableAsync()).DeleteAsync();
        }

        public async Task<LatestPackageLeaf> GetOrNullAsync(string id, string version)
        {
            return await (await GetTableAsync()).GetEntityOrNullAsync<LatestPackageLeaf>(
                LatestPackageLeaf.GetPartitionKey(id),
                LatestPackageLeaf.GetRowKey(version));
        }

        internal async Task<TableClientWithRetryContext> GetTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.LatestPackageLeafTableName);
        }
    }
}

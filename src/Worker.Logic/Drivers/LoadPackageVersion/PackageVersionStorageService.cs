// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker.LoadPackageVersion
{
    public class PackageVersionStorageService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly CatalogClient _catalogClient;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public PackageVersionStorageService(
            ServiceClientFactory serviceClientFactory,
            CatalogClient catalogClient,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _catalogClient = catalogClient;
            _telemetryClient = telemetryClient;
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

        public async Task<PackageVersionStorage> GetLatestPackageLeafStorageAsync()
        {
            return new PackageVersionStorage(await GetTableAsync(), _catalogClient);
        }

        public async Task<IReadOnlyList<PackageVersionEntity>> GetAsync(string packageId)
        {
            return await (await GetTableAsync())
                .QueryAsync<PackageVersionEntity>(x => x.PartitionKey == PackageVersionEntity.GetPartitionKey(packageId))
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());
        }

        internal async Task<TableClientWithRetryContext> GetTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.PackageVersionTableName);
        }
    }
}

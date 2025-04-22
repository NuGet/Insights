// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker.LoadBucketedPackage
{
    public class BucketedPackageService
    {
        private readonly ContainerInitializationState _initializationState;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public BucketedPackageService(
            ServiceClientFactory serviceClientFactory,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _initializationState = ContainerInitializationState.Table(serviceClientFactory, options.Value.BucketedPackageTableName);
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

        internal async Task<TableClientWithRetryContext> GetTableAsync()
        {
            var tableServiceClient = await _serviceClientFactory.GetTableServiceClientAsync();
            var table = tableServiceClient.GetTableClient(_options.Value.BucketedPackageTableName);
            return table;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Core;
using Azure.Data.Tables;
using NuGet.Insights.MemoryStorage;

#nullable enable

namespace NuGet.Insights
{
    public interface ITableClientFactory
    {
        TableServiceClient GetServiceClient();
    }

    public class MemoryTableClientFactory : ITableClientFactory
    {
        private readonly TimeProvider _timeProvider;
        private readonly MemoryTableServiceStore _store;

        public MemoryTableClientFactory(TimeProvider timeProvider, MemoryTableServiceStore store)
        {
            _timeProvider = timeProvider;
            _store = store;
        }

        public TableServiceClient GetServiceClient() => new MemoryTableServiceClient(_timeProvider, _store);
    }

    public class DevelopmentTableClientFactory : ITableClientFactory
    {
        private readonly TableClientOptions _options;

        public DevelopmentTableClientFactory(TableClientOptions options)
        {
            _options = options;
        }

        public TableServiceClient GetServiceClient() => new TableServiceClient(StorageUtility.DevelopmentConnectionString, _options);
    }

    public class TokenCredentialTableClientFactory : ITableClientFactory
    {
        private readonly Uri _serviceUri;
        private readonly TokenCredential _credential;
        private readonly TableClientOptions _options;

        public TokenCredentialTableClientFactory(Uri serviceUri, TokenCredential credential, TableClientOptions options)
        {
            _serviceUri = serviceUri;
            _credential = credential;
            _options = options;
        }

        public TableServiceClient GetServiceClient() => new TableServiceClient(_serviceUri, _credential, _options);

    }

    public class SharedKeyCredentialTableClientFactory : ITableClientFactory
    {
        private readonly Uri _serviceUri;
        private readonly TableSharedKeyCredential _credential;
        private readonly TableClientOptions _options;

        public SharedKeyCredentialTableClientFactory(Uri serviceUri, TableSharedKeyCredential credential, TableClientOptions options)
        {
            _serviceUri = serviceUri;
            _credential = credential;
            _options = options;
        }

        public TableServiceClient GetServiceClient() => new TableServiceClient(_serviceUri, _credential, _options);
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Core;
using Azure.Storage;
using Azure.Storage.Queues;
using NuGet.Insights.MemoryStorage;

#nullable enable

namespace NuGet.Insights
{
    public interface IQueueClientFactory
    {
        QueueServiceClient GetServiceClient();
    }

    public class MemoryQueueClientFactory : IQueueClientFactory
    {
        public static MemoryQueueServiceStore SharedStore { get; } = new();

        public QueueServiceClient GetServiceClient() => new MemoryQueueServiceClient(SharedStore);
    }

    public class DevelopmentQueueClientFactory : IQueueClientFactory
    {
        private readonly QueueClientOptions _options;

        public DevelopmentQueueClientFactory(QueueClientOptions options)
        {
            _options = options;
        }

        public QueueServiceClient GetServiceClient() => new QueueServiceClient(StorageUtility.DevelopmentConnectionString, _options);
    }

    public class TokenCredentialQueueClientFactory : IQueueClientFactory
    {
        private readonly Uri _serviceUri;
        private readonly TokenCredential _credential;
        private readonly QueueClientOptions _options;

        public TokenCredentialQueueClientFactory(Uri serviceUri, TokenCredential credential, QueueClientOptions options)
        {
            _serviceUri = serviceUri;
            _credential = credential;
            _options = options;
        }

        public QueueServiceClient GetServiceClient() => new QueueServiceClient(_serviceUri, _credential, _options);

    }

    public class SharedKeyCredentialQueueClientFactory : IQueueClientFactory
    {
        private readonly Uri _serviceUri;
        private readonly StorageSharedKeyCredential _credential;
        private readonly QueueClientOptions _options;

        public SharedKeyCredentialQueueClientFactory(Uri serviceUri, StorageSharedKeyCredential credential, QueueClientOptions options)
        {
            _serviceUri = serviceUri;
            _credential = credential;
            _options = options;
        }

        public QueueServiceClient GetServiceClient() => new QueueServiceClient(_serviceUri, _credential, _options);
    }
}

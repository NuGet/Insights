// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryQueueServiceClient : QueueServiceClient
    {
        private readonly MemoryQueueServiceStore _store;
        private readonly QueueClientOptions _options;

        public MemoryQueueServiceClient(MemoryQueueServiceStore store) : this(
            store,
            StorageUtility.GetQueueEndpoint(StorageUtility.MemoryStorageAccountName),
            MemoryTokenCredential.Instance,
            new QueueClientOptions().AddBrokenTransport())
        {
        }

        private MemoryQueueServiceClient(MemoryQueueServiceStore store, Uri serviceUri, TokenCredential tokenCredential, QueueClientOptions options)
            : base(serviceUri, tokenCredential, options.AddBrokenTransport())
        {
            _store = store;
            _options = options;
        }

        public override QueueClient GetQueueClient(
            string queueName)
        {
            var uri = Uri.AppendToPath(queueName);
            return new MemoryQueueClient(_store, uri, _options);
        }

        public override AsyncPageable<QueueItem> GetQueuesAsync(
            QueueTraits traits = QueueTraits.None,
            string? prefix = null,
            CancellationToken cancellationToken = default)
        {
            return AsyncPageable<QueueItem>.FromPages(GetQueuePages(traits, prefix));
        }
    }
}
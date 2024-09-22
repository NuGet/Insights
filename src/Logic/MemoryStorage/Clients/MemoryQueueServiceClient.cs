// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Storage;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryQueueServiceClient : QueueServiceClient
    {
        private readonly StorageSharedKeyCredential? _sharedKeyCredential;
        private readonly TokenCredential? _tokenCredential;

        private static readonly MemoryQueueServiceStore SharedStore = new MemoryQueueServiceStore();

        public MemoryQueueServiceClient(
            Uri serviceUri,
            StorageSharedKeyCredential credential,
            QueueClientOptions options) : base(serviceUri, credential, options.AddBrokenTransport())
        {
            _sharedKeyCredential = credential;
            Options = options;
        }

        public MemoryQueueServiceClient(
            Uri serviceUri,
            TokenCredential credential,
            QueueClientOptions options) : base(serviceUri, credential, options.AddBrokenTransport())
        {
            _tokenCredential = credential;
            Options = options;
        }

        public QueueClientOptions Options { get; }
        public MemoryQueueServiceStore Store { get; } = SharedStore;

        public override QueueClient GetQueueClient(
            string queueName)
        {
            var uri = Uri.AppendToPath(queueName);
            if (_sharedKeyCredential is not null)
            {
                return new MemoryQueueClient(this, uri, _sharedKeyCredential);
            }
            else
            {
                return new MemoryQueueClient(this, uri, _tokenCredential!);
            }
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

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryQueueClient : QueueClient
    {
        private readonly MemoryQueueStore _store;

        public MemoryQueueClient(TimeProvider timeProvider, MemoryQueueServiceStore parent, Uri uri, QueueClientOptions options)
            : base(uri, new MemoryTokenCredential(timeProvider), options.AddBrokenTransport())
        {
            _store = parent.GetQueue(Name);
        }

        public override Task<Response<bool>> ExistsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistsResponse());
        }

        public override Task<Response> CreateIfNotExistsAsync(
            IDictionary<string, string>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateIfNotExistsResponse(metadata));
        }

        public override Task<Response<SendReceipt>> SendMessageAsync(
            string messageText,
            TimeSpan? visibilityTimeout = null,
            TimeSpan? timeToLive = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SendMessageResponse(messageText, visibilityTimeout, timeToLive));
        }

        public override Task<Response<QueueMessage[]>> ReceiveMessagesAsync(
            int? maxMessages = null,
            TimeSpan? visibilityTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ReceiveMessagesResponse(maxMessages, visibilityTimeout));
        }

        public override Task<Response<QueueMessage?>> ReceiveMessageAsync(
            TimeSpan? visibilityTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ReceiveMessageResponse(visibilityTimeout));
        }

        public override Task<Response> DeleteMessageAsync(
            string messageId,
            string popReceipt,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DeleteMessageResponse(messageId, popReceipt));
        }

        public override Task<Response> DeleteAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DeleteResponse());
        }

        public override Task<Response<QueueProperties>> GetPropertiesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetPropertiesResponse());
        }

        public override Task<Response<PeekedMessage[]>> PeekMessagesAsync(
            int? maxMessages = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PeekMessagesResponse(maxMessages));
        }

        public override Task<Response<PeekedMessage?>> PeekMessageAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PeekMessageResponse());
        }

        public override Task<Response<UpdateReceipt>> UpdateMessageAsync(
            string messageId,
            string popReceipt,
            string? messageText = null,
            TimeSpan visibilityTimeout = default,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(UpdateMessageResponse(messageId, popReceipt, messageText, visibilityTimeout));
        }
    }
}

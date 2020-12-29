using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class MessageEnqueuer
    {
        private delegate Task AddAsync(IReadOnlyList<string> messages, TimeSpan notBefore);

        private readonly SchemaSerializer _serializer;
        private readonly IMessageBatcher _batcher;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;
        private readonly ILogger<MessageEnqueuer> _logger;

        public MessageEnqueuer(
            SchemaSerializer serializer,
            IMessageBatcher batcher,
            IRawMessageEnqueuer rawMessageEnqueuer,
            ILogger<MessageEnqueuer> logger)
        {
            _serializer = serializer;
            _batcher = batcher;
            _rawMessageEnqueuer = rawMessageEnqueuer;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _rawMessageEnqueuer.InitializeAsync();
        }

        public Task EnqueueAsync<T>(IReadOnlyList<T> messages) => EnqueueAsync(messages, TimeSpan.Zero);
        public Task EnqueueAsync<T>(IReadOnlyList<T> messages, TimeSpan notBefore) => EnqueueAsync(messages, _serializer.GetSerializer<T>(), notBefore);
        internal Task EnqueueAsync<T>(IReadOnlyList<T> messages, ISchemaSerializer<T> serializer, TimeSpan notBefore) => EnqueueAsync(_rawMessageEnqueuer.AddAsync, messages, serializer, notBefore);

        public Task EnqueuePoisonAsync<T>(IReadOnlyList<T> messages) => EnqueuePoisonAsync(messages, TimeSpan.Zero);
        public Task EnqueuePoisonAsync<T>(IReadOnlyList<T> messages, TimeSpan notBefore) => EnqueueAsync(_rawMessageEnqueuer.AddPoisonAsync, messages, _serializer.GetSerializer<T>(), notBefore);

        private async Task EnqueueAsync<T>(AddAsync addAsync, IReadOnlyList<T> messages, ISchemaSerializer<T> serializer, TimeSpan notBefore)
        {
            if (messages.Count == 0)
            {
                return;
            }

            var batches = await _batcher.BatchOrNullAsync(messages, serializer);
            if (batches != null)
            {
                await EnqueueAsync(batches, notBefore);
                return;
            }

            var bulkEnqueueStrategy = _rawMessageEnqueuer.BulkEnqueueStrategy;
            if (!bulkEnqueueStrategy.IsEnabled || messages.Count < bulkEnqueueStrategy.Threshold)
            {
                var serializedMessages = messages.Select(m => serializer.SerializeMessage(m).AsString()).ToList();
                await addAsync(serializedMessages, notBefore);
            }
            else
            {
                var batch = new List<JToken>();
                var batchMessage = new HomogeneousBulkEnqueueMessage
                {
                    SchemaName = serializer.Name,
                    SchemaVersion = serializer.LatestVersion,
                    Messages = batch,
                    NotBefore = notBefore <= TimeSpan.Zero ? (TimeSpan?)null : notBefore,
                };
                var emptyBatchMessageLength = GetMessageLength(batchMessage);
                var batchMessageLength = emptyBatchMessageLength;

                for (int i = 0; i < messages.Count; i++)
                {
                    var innerData = serializer.SerializeData(messages[i]);
                    var innerDataLength = GetMessageLength(innerData);

                    if (!batch.Any())
                    {
                        batch.Add(innerData.AsJToken());
                        batchMessageLength += innerDataLength;
                    }
                    else
                    {
                        var newBatchMessageLength = batchMessageLength + ",".Length + innerDataLength;
                        if (newBatchMessageLength > bulkEnqueueStrategy.MaxSize)
                        {
                            await EnqueueBulkEnqueueMessageAsync(addAsync, batchMessage, batchMessageLength);
                            batch.Clear();
                            batch.Add(innerData.AsJToken());
                            batchMessageLength = emptyBatchMessageLength + innerDataLength;
                        }
                        else
                        {
                            batch.Add(innerData.AsJToken());
                            batchMessageLength = newBatchMessageLength;
                        }
                    }
                }

                if (batch.Count > 0)
                {
                    await EnqueueBulkEnqueueMessageAsync(addAsync, batchMessage, batchMessageLength);
                }
            }
        }

        private async Task EnqueueBulkEnqueueMessageAsync(AddAsync addAsync, HomogeneousBulkEnqueueMessage batchMessage, int expectedLength)
        {
            var rawMessage = _serializer.Serialize(batchMessage).AsString();
            if (GetMessageLength(rawMessage) != expectedLength)
            {
                throw new InvalidOperationException(
                    $"The bulk enqueue message had an unexpected size. " +
                    $"Expected: {expectedLength}. " +
                    $"Actual: {rawMessage.Length}");
            }

            _logger.LogInformation("Enqueueing a bulk enqueue message containing {Count} individual messages.", batchMessage.Messages.Count);
            await addAsync(new[] { rawMessage }, TimeSpan.Zero);
        }

        private int GetMessageLength(HomogeneousBulkEnqueueMessage batchMessage) => GetMessageLength(_serializer.Serialize(batchMessage));
        private static int GetMessageLength(ISerializedEntity innerMessage) => GetMessageLength(innerMessage.AsString());
        private static int GetMessageLength(string message) => Encoding.UTF8.GetByteCount(message);
    }
}

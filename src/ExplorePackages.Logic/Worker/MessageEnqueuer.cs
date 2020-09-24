using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class MessageEnqueuer
    {
        private readonly SchemaSerializer _serializer;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;
        private readonly ILogger<MessageEnqueuer> _logger;

        public MessageEnqueuer(
            SchemaSerializer serializer,
            IRawMessageEnqueuer rawMessageEnqueuer,
            ILogger<MessageEnqueuer> logger)
        {
            _serializer = serializer;
            _rawMessageEnqueuer = rawMessageEnqueuer;
            _logger = logger;
        }

        public async Task EnqueueAsync(IReadOnlyList<CatalogIndexScanMessage> messages) => await EnqueueAsync(messages, TimeSpan.Zero);
        public async Task EnqueueAsync(IReadOnlyList<CatalogIndexScanMessage> messages, TimeSpan notBefore)
        {
            await EnqueueAsync(messages, m => _serializer.Serialize(m), notBefore);
        }

        public async Task EnqueueAsync(IReadOnlyList<CatalogPageScanMessage> messages) => await EnqueueAsync(messages, TimeSpan.Zero);
        public async Task EnqueueAsync(IReadOnlyList<CatalogPageScanMessage> messages, TimeSpan notBefore)
        {
            await EnqueueAsync(messages, m => _serializer.Serialize(m), notBefore);
        }

        public async Task EnqueueAsync(IReadOnlyList<CatalogLeafScanMessage> messages) => await EnqueueAsync(messages, TimeSpan.Zero);
        public async Task EnqueueAsync(IReadOnlyList<CatalogLeafScanMessage> messages, TimeSpan notBefore)
        {
            await EnqueueAsync(messages, m => _serializer.Serialize(m), notBefore);
        }

        public async Task EnqueueAsync<T>(IReadOnlyList<T> messages, Func<T, ISerializedEntity> serialize)
        {
            await EnqueueAsync(messages, serialize, TimeSpan.Zero);
        }

        public async Task EnqueueAsync<T>(IReadOnlyList<T> messages, Func<T, ISerializedEntity> serialize, TimeSpan notBefore)
        {
            if (messages.Count == 0)
            {
                return;
            }

            var bulkEnqueueStrategy = _rawMessageEnqueuer.BulkEnqueueStrategy;
            if (!bulkEnqueueStrategy.IsEnabled || messages.Count < bulkEnqueueStrategy.Threshold)
            {
                var serializedMessages = messages.Select(m => serialize(m).AsString()).ToList();
                await _rawMessageEnqueuer.AddAsync(serializedMessages, notBefore);
            }
            else
            {
                var batch = new List<JToken>();
                var batchMessage = new BulkEnqueueMessage { Messages = batch, NotBefore = notBefore };
                var emptyBatchMessageLength = GetMessageLength(batchMessage);
                var batchMessageLength = emptyBatchMessageLength;

                for (int i = 0; i < messages.Count; i++)
                {
                    var innerMessage = serialize(messages[i]);
                    var innerMessageLength = GetMessageLength(innerMessage);

                    if (!batch.Any())
                    {
                        batch.Add(innerMessage.AsJToken());
                        batchMessageLength += innerMessageLength;
                    }
                    else
                    {
                        var newBatchMessageLength = batchMessageLength + ",".Length + innerMessageLength;
                        if (newBatchMessageLength > bulkEnqueueStrategy.MaxSize)
                        {
                            await EnqueueBulkEnqueueMessageAsync(batchMessage, batchMessageLength);
                            batch.Clear();
                            batch.Add(innerMessage.AsJToken());
                            batchMessageLength = emptyBatchMessageLength + innerMessageLength;
                        }
                        else
                        {
                            batch.Add(innerMessage.AsJToken());
                            batchMessageLength = newBatchMessageLength;
                        }
                    }
                }

                if (batch.Count > 0)
                {
                    await EnqueueBulkEnqueueMessageAsync(batchMessage, batchMessageLength);
                }
            }
        }

        private async Task EnqueueBulkEnqueueMessageAsync(BulkEnqueueMessage batchMessage, int expectedLength)
        {
            var bytes = _serializer.Serialize(batchMessage).AsString();
            if (bytes.Length != expectedLength)
            {
                throw new InvalidOperationException(
                    $"The bulk enqueue message had an unexpected size. " +
                    $"Expected: {expectedLength}. " +
                    $"Actual: {bytes.Length}");
            }

            _logger.LogInformation("Enqueueing a bulk enqueue message containing {Count} individual messages.", batchMessage.Messages.Count);
            await _rawMessageEnqueuer.AddAsync(new[] { bytes });
        }

        private int GetMessageLength(BulkEnqueueMessage batchMessage)
        {
            return GetMessageLength(_serializer.Serialize(batchMessage));
        }

        private static int GetMessageLength(ISerializedEntity innerMessage)
        {
            return Encoding.UTF8.GetByteCount(innerMessage.AsString());
        }
    }
}

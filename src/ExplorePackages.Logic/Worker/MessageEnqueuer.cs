using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class MessageEnqueuer
    {
        private readonly MessageSerializer _messageSerializer;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;

        /// <summary>
        /// This is the effective maximum message size for a byte array message body. This is because the SDK encodes
        /// bytes as base64 and 49,152 bytes becomes 65,536 bytes of base64 text (the actual maximum message size for
        /// Azure Queue Storage).
        /// </summary>
        private const int MaximumMessageSize = 49152;

        public MessageEnqueuer(MessageSerializer messageSerializer, IRawMessageEnqueuer rawMessageEnqueuer)
        {
            _messageSerializer = messageSerializer;
            _rawMessageEnqueuer = rawMessageEnqueuer;
        }

        public async Task EnqueueAsync(IReadOnlyList<CatalogIndexScanMessage> messages)
        {
            await EnqueueAsync(messages, m => _messageSerializer.Serialize(m));
        }

        public async Task EnqueueAsync(IReadOnlyList<CatalogPageScanMessage> messages)
        {
            await EnqueueAsync(messages, m => _messageSerializer.Serialize(m));
        }

        public async Task EnqueueAsync(IReadOnlyList<CatalogLeafMessage> messages)
        {
            await EnqueueAsync(messages, m => _messageSerializer.Serialize(m));
        }

        public async Task EnqueueAsync<T>(IReadOnlyList<T> messages, Func<T, ISerializedMessage> serialize)
        {
            const int batchSize = 100;

            await EnqueueAsync(messages, serialize, batchSize);
        }

        private async Task EnqueueAsync<T>(IReadOnlyList<T> messages, Func<T, ISerializedMessage> serialize, int batchThreshold)
        {
            if (messages.Count < batchThreshold)
            {
                foreach (var message in messages)
                {
                    await _rawMessageEnqueuer.AddAsync(serialize(message).AsBytes());
                }
            }
            else
            {
                var batch = new List<JToken>();
                var batchMessage = new BulkEnqueueMessage { Messages = batch };
                var emptyBatchMessageLength = GetMessageByteLength(batchMessage);
                var batchMessageLength = emptyBatchMessageLength;

                for (int i = 0; i < messages.Count; i++)
                {
                    var innerMessage = serialize(messages[i]);
                    var innerMessageLength = innerMessage.AsBytes().Length;

                    if (!batch.Any())
                    {
                        batch.Add(innerMessage.AsJToken());
                        batchMessageLength += innerMessageLength;
                    }
                    else
                    {
                        var newBatchMessageLength = batchMessageLength + ",".Length + innerMessageLength;
                        if (ByteArrayLengthToBase64Length(newBatchMessageLength) > MaximumMessageSize)
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
            var bytes = _messageSerializer.Serialize(batchMessage).AsBytes();
            if (bytes.Length != expectedLength)
            {
                throw new InvalidOperationException(
                    $"The bulk enqueue message had an unexpected size. " +
                    $"Expected: {expectedLength}. " +
                    $"Actual: {bytes.Length}");
            }

            await _rawMessageEnqueuer.AddAsync(bytes);
        }

        private int GetMessageByteLength(BulkEnqueueMessage batchMessage)
        {
            return _messageSerializer.Serialize(batchMessage).AsBytes().Length;
        }

        private static int ByteArrayLengthToBase64Length(int length)
        {
            return ((length + 2) / 3) * 4;
        }
    }
}

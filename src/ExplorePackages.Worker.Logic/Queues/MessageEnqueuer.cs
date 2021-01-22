using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class MessageEnqueuer : IMessageEnqueuer
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
        public Task EnqueueAsync<T>(IReadOnlyList<T> messages, Func<T, IReadOnlyList<T>> split) => EnqueueAsync(_rawMessageEnqueuer.AddAsync, messages, split, _serializer.GetSerializer<T>(), TimeSpan.Zero);
        public Task EnqueueAsync<T>(IReadOnlyList<T> messages, TimeSpan notBefore) => EnqueueAsync(_rawMessageEnqueuer.AddAsync, messages, NoSplit, _serializer.GetSerializer<T>(), notBefore);
        internal Task EnqueueAsync<T>(IReadOnlyList<T> messages, ISchemaSerializer<T> serializer, TimeSpan notBefore) => EnqueueAsync(_rawMessageEnqueuer.AddAsync, messages, NoSplit, serializer, notBefore);

        public Task EnqueuePoisonAsync<T>(IReadOnlyList<T> messages) => EnqueuePoisonAsync(messages, TimeSpan.Zero);
        public Task EnqueuePoisonAsync<T>(IReadOnlyList<T> messages, TimeSpan notBefore) => EnqueueAsync(_rawMessageEnqueuer.AddPoisonAsync, messages, NoSplit, _serializer.GetSerializer<T>(), notBefore);

        private async Task EnqueueAsync<T>(
            AddAsync addAsync,
            IReadOnlyList<T> messages,
            Func<T, IReadOnlyList<T>> split,
            ISchemaSerializer<T> serializer,
            TimeSpan notBefore)
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

            if (!_rawMessageEnqueuer.BulkEnqueueStrategy.IsEnabled || messages.Count < _rawMessageEnqueuer.BulkEnqueueStrategy.Threshold)
            {
                var serializedMessages = new List<string>();
                var messagesToAdd = new Queue<T>(messages);

                while (messagesToAdd.Any())
                {
                    var currentMessage = messagesToAdd.Dequeue();
                    var serializedMessage = serializer.SerializeMessage(currentMessage);
                    if (TrySplit(currentMessage, serializedMessage, split, messagesToAdd))
                    {
                        continue;
                    }

                    serializedMessages.Add(serializedMessage.AsString());
                }

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
                    NotBefore = notBefore <= TimeSpan.Zero ? null : notBefore,
                };
                var emptyBatchMessageLength = GetMessageLength(batchMessage);
                var batchMessageLength = emptyBatchMessageLength;

                var messagesToAdd = new Queue<T>(messages);

                while (messagesToAdd.Any())
                {
                    var message = messagesToAdd.Dequeue();
                    var innerData = serializer.SerializeData(message);
                    if (TrySplit(message, innerData, split, messagesToAdd))
                    {
                        continue;
                    }

                    var innerDataLength = GetMessageLength(innerData);

                    if (!batch.Any())
                    {
                        batch.Add(innerData.AsJToken());
                        batchMessageLength += innerDataLength;
                    }
                    else
                    {
                        var newBatchMessageLength = batchMessageLength + ",".Length + innerDataLength;
                        if (newBatchMessageLength > _rawMessageEnqueuer.MaxMessageSize)
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

        private IReadOnlyList<T> NoSplit<T>(T message) => null;

        private bool TrySplit<T>(T message, ISerializedEntity serializedMessage, Func<T, IReadOnlyList<T>> split, Queue<T> messagesToAdd)
        {
            var serializedMessageLength = GetMessageLength(serializedMessage);
            if (serializedMessageLength > _rawMessageEnqueuer.MaxMessageSize)
            {
                var splitMessages = split(message);
                _logger.LogWarning(
                    "Message of type {Type} and length {Length} is bigger than the limit {MaxLength}. Attempting to split.",
                    message.GetType().FullName,
                    serializedMessageLength,
                    _rawMessageEnqueuer.MaxMessageSize);
                if (splitMessages != null)
                {
                    _logger.LogWarning("Message was split into {MessageCount} messages.", splitMessages.Count);
                    foreach (var splitMessage in splitMessages)
                    {
                        messagesToAdd.Enqueue(splitMessage);
                    }

                    return true;
                }
            }

            return false;
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

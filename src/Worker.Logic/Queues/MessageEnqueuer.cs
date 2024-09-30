// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class MessageEnqueuer : IMessageEnqueuer
    {
        private static readonly ConcurrentDictionary<Type, QueueType> TypeToQueue = new ConcurrentDictionary<Type, QueueType>();
        private static readonly ConcurrentDictionary<string, QueueType> SchemaNameToQueue = new ConcurrentDictionary<string, QueueType>();

        private delegate Task AddAsync(QueueType queue, IReadOnlyList<string> messages, TimeSpan notBefore);

        private readonly SchemaSerializer _serializer;
        private readonly IMessageBatcher _batcher;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<MessageEnqueuer> _logger;
        private readonly IMetric _enqueueMetric;

        public MessageEnqueuer(
            SchemaSerializer serializer,
            IMessageBatcher batcher,
            IRawMessageEnqueuer rawMessageEnqueuer,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<MessageEnqueuer> logger)
        {
            _serializer = serializer;
            _batcher = batcher;
            _rawMessageEnqueuer = rawMessageEnqueuer;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;

            _enqueueMetric = _telemetryClient.GetMetric($"{nameof(MessageEnqueuer)}.Enqueue", "QueueType", "IsPoison", "SchemaName", "IsBulkEnqueue");
        }

        public async Task InitializeAsync()
        {
            await _rawMessageEnqueuer.InitializeAsync();
        }

        public Task EnqueueAsync<T>(IReadOnlyList<T> messages)
        {
            return EnqueueAsync(messages, TimeSpan.Zero);
        }

        public Task EnqueueAsync<T>(IReadOnlyList<T> messages, Func<T, IReadOnlyList<T>> split)
        {
            return EnqueueAsync(isPoison: false, _rawMessageEnqueuer.AddAsync, messages, split, _serializer.GetSerializer<T>(), TimeSpan.Zero);
        }

        public Task EnqueueAsync<T>(IReadOnlyList<T> messages, TimeSpan notBefore)
        {
            return EnqueueAsync(isPoison: false, _rawMessageEnqueuer.AddAsync, messages, NoSplit, _serializer.GetSerializer<T>(), notBefore);
        }

        internal Task EnqueueAsync<T>(IReadOnlyList<T> messages, ISchemaSerializer<T> serializer, TimeSpan notBefore)
        {
            return EnqueueAsync(isPoison: false, _rawMessageEnqueuer.AddAsync, messages, NoSplit, serializer, notBefore);
        }

        public Task EnqueuePoisonAsync<T>(IReadOnlyList<T> messages)
        {
            return EnqueuePoisonAsync(messages, TimeSpan.Zero);
        }

        public Task EnqueuePoisonAsync<T>(IReadOnlyList<T> messages, TimeSpan notBefore)
        {
            return EnqueueAsync(isPoison: true, _rawMessageEnqueuer.AddPoisonAsync, messages, NoSplit, _serializer.GetSerializer<T>(), notBefore);
        }

        public async Task EnqueueAsync<T>(
            QueueType queue,
            bool isPoison,
            IReadOnlyList<T> messages,
            Func<T, IReadOnlyList<T>> split,
            TimeSpan notBefore)
        {
            await EnqueueAsync(
                queue,
                isPoison,
                isPoison ? _rawMessageEnqueuer.AddPoisonAsync : _rawMessageEnqueuer.AddAsync,
                messages, split,
                _serializer.GetSerializer<T>(),
                notBefore);
        }

        private async Task EnqueueAsync<T>(
            bool isPoison,
            AddAsync addAsync,
            IReadOnlyList<T> messages,
            Func<T, IReadOnlyList<T>> split,
            ISchemaSerializer<T> serializer,
            TimeSpan notBefore)
        {
            // Determine which queue the messages should go into, by type.
            var queue = GetQueueType<T>();

            await EnqueueAsync(queue, isPoison, addAsync, messages, split, serializer, notBefore);
        }

        private async Task EnqueueAsync<T>(
            QueueType queue,
            bool isPoison,
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

            // Batch the messages
            var batches = await _batcher.BatchOrNullAsync(messages, serializer);
            if (batches != null)
            {
                await EnqueueAsync(
                    queue,
                    isPoison,
                    addAsync,
                    batches,
                    NoSplit,
                    _serializer.GetSerializer<HomogeneousBatchMessage>(),
                    notBefore);
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

                TrackEnqueue(queue, isPoison, serializer.Name, isBulkEnqueue: false, serializedMessages.Count);
                await addAsync(queue, serializedMessages, notBefore);
            }
            else
            {
                var batch = new List<JsonElement>();
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
                        batch.Add(innerData.AsJsonElement());
                        batchMessageLength += innerDataLength;
                    }
                    else
                    {
                        var newBatchMessageLength = batchMessageLength + ",".Length + innerDataLength;
                        if (newBatchMessageLength > _rawMessageEnqueuer.MaxMessageSize || batch.Count >= _options.Value.MaxBulkEnqueueMessageCount)
                        {
                            await EnqueueBulkEnqueueMessageAsync(isPoison, serializer.Name, addAsync, batchMessage, batchMessageLength);
                            batch.Clear();
                            batch.Add(innerData.AsJsonElement());
                            batchMessageLength = emptyBatchMessageLength + innerDataLength;
                        }
                        else
                        {
                            batch.Add(innerData.AsJsonElement());
                            batchMessageLength = newBatchMessageLength;
                        }
                    }
                }

                if (batch.Count > 0)
                {
                    await EnqueueBulkEnqueueMessageAsync(isPoison, serializer.Name, addAsync, batchMessage, batchMessageLength);
                }
            }
        }

        public QueueType GetQueueType<TMessage>()
        {
            return GetQueueType(typeof(TMessage));
        }

        public QueueType GetQueueType(Type messageType)
        {
            return TypeToQueue.GetOrAdd(messageType, GetQueueTypeUncached);
        }

        public QueueType GetQueueType(string schemaName)
        {
            return SchemaNameToQueue.GetOrAdd(schemaName, s =>
            {
                var deserializer = _serializer.GetDeserializer(s);
                return TypeToQueue.GetOrAdd(deserializer.Type, GetQueueTypeUncached);
            });
        }

        public async Task<int> GetMaxApproximateMessageCountAsync(params Type[] messageTypes)
        {
            if (messageTypes.Length == 0)
            {
                throw new ArgumentException("At least one message type is required.", nameof(messageTypes));
            }

            var queues = new HashSet<QueueType>();
            var max = int.MinValue;
            foreach (var messageType in messageTypes)
            {
                var queueType = GetQueueType(messageType);
                if (queues.Add(queueType))
                {
                    var messageCount = await _rawMessageEnqueuer.GetApproximateMessageCountAsync(queueType);
                    if (messageCount > max)
                    {
                        max = messageCount;
                    }
                }
            }

            return max;
        }

        private QueueType GetQueueTypeUncached(Type messageType)
        {
            if (messageType == typeof(HomogeneousBulkEnqueueMessage)
                || (messageType.IsGenericType && messageType.GetGenericTypeDefinition() == typeof(TableScanMessage<>)))
            {
                return QueueType.Expand;
            }

            return QueueType.Work;
        }

        private IReadOnlyList<T> NoSplit<T>(T message)
        {
            return null;
        }

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

        private async Task EnqueueBulkEnqueueMessageAsync(bool isPoison, string schemaName, AddAsync addAsync, HomogeneousBulkEnqueueMessage batchMessage, int expectedLength)
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
            TrackEnqueue(QueueType.Expand, isPoison, schemaName, isBulkEnqueue: true, 1);
            await addAsync(QueueType.Expand, [rawMessage], TimeSpan.Zero);

        }

        private void TrackEnqueue(QueueType queue, bool isPoison, string schemaName, bool isBulkEnqueue, int count)
        {
            _enqueueMetric.TrackValue(
                count,
                queue.ToString(),
                isPoison ? "true" : "false",
                schemaName,
                isBulkEnqueue ? "true" : "false");
        }

        private int GetMessageLength(HomogeneousBulkEnqueueMessage batchMessage)
        {
            return GetMessageLength(_serializer.Serialize(batchMessage));
        }

        private static int GetMessageLength(ISerializedEntity innerMessage)
        {
            return GetMessageLength(innerMessage.AsString());
        }

        private static int GetMessageLength(string message)
        {
            return Encoding.UTF8.GetByteCount(message);
        }
    }
}

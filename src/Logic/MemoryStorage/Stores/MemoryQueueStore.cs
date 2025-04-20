// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Queues.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public class MemoryQueueStore
    {
        private ConcurrentDictionary<string, MemoryQueueMessageStore> _messages = new();
        private ConcurrentQueue<string> _queue = new();

        private readonly object _lock = new();
        private readonly TimeProvider _timeProvider;
        private string _name;
        private bool _exists;

        public MemoryQueueStore(TimeProvider timeProvider, string name)
        {
            _timeProvider = timeProvider;
            _name = name;
        }

        public virtual bool Exists()
        {
            lock (_lock)
            {
                return _exists;
            }
        }

        public virtual StorageResult<QueueItem> GetQueueItem(QueueTraits traits)
        {
            lock (_lock)
            {
                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                if (traits != QueueTraits.None)
                {
                    throw new NotImplementedException();
                }

                return new(StorageResultType.Success, QueuesModelFactory.QueueItem(_name, metadata: null));
            }
        }

        public virtual StorageResultType CreateIfNotExists(IDictionary<string, string>? metadata)
        {
            lock (_lock)
            {
                if (metadata is not null)
                {
                    throw new NotImplementedException();
                }

                if (_exists)
                {
                    return StorageResultType.AlreadyExists;
                }

                _exists = true;
                return StorageResultType.Success;
            }
        }

        public virtual StorageResult<SendReceipt> SendMessage(
            string messageText,
            TimeSpan? visibilityTimeout,
            TimeSpan? timeToLive)
        {
            lock (_lock)
            {
                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                if (timeToLive.HasValue)
                {
                    throw new NotImplementedException();
                }

                var messageId = $"message-id-{Guid.NewGuid()}";
                var message = new MemoryQueueMessageStore(
                    _timeProvider,
                    messageId,
                    messageText,
                    visibilityTimeout.GetValueOrDefault(TimeSpan.Zero));

                if (!_messages.TryAdd(messageId, message))
                {
                    throw new NotImplementedException();
                }

                _queue.Enqueue(messageId);

                return new(StorageResultType.Success, message.GetSendReceipt());
            }
        }

        public virtual StorageResult<QueueMessage[]> ReceiveMessages(int? maxMessages, TimeSpan? visibilityTimeout)
        {
            lock (_lock)
            {
                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                if (maxMessages.HasValue && (maxMessages.Value < 1 || maxMessages.Value > 32))
                {
                    throw new ArgumentOutOfRangeException(nameof(maxMessages));
                }

                if (visibilityTimeout.HasValue && visibilityTimeout.Value < TimeSpan.FromSeconds(1))
                {
                    throw new NotImplementedException();
                }

                var maxMessagesValue = maxMessages.GetValueOrDefault(1);
                var visibilityTimeoutValue = visibilityTimeout.GetValueOrDefault(TimeSpan.FromSeconds(30));

                var attempted = new List<string>();
                var output = new List<QueueMessage>();
                while (_queue.TryDequeue(out var messageId))
                {
                    if (!_messages.TryGetValue(messageId, out var message))
                    {
                        continue;
                    }

                    attempted.Add(messageId);

                    var queueMessage = message.GetQueueMessage(visibilityTimeoutValue);
                    if (queueMessage is not null)
                    {
                        output.Add(queueMessage);
                        if (output.Count >= maxMessagesValue)
                        {
                            break;
                        }
                    }
                }

                foreach (var messageId in attempted)
                {
                    _queue.Enqueue(messageId);
                }

                if (_messages.Keys.Any(x => !_queue.Contains(x)))
                {
                    throw new InvalidOperationException();
                }

                return new(StorageResultType.Success, output.ToArray());
            }
        }

        public virtual StorageResult<PeekedMessage[]> PeekMessages(int? maxMessages)
        {
            lock (_lock)
            {
                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                if (maxMessages.HasValue && (maxMessages.Value < 1 || maxMessages.Value > 32))
                {
                    throw new ArgumentOutOfRangeException(nameof(maxMessages));
                }

                var maxMessagesValue = maxMessages.GetValueOrDefault(1);
                var output = new List<PeekedMessage>();
                foreach (var messageId in _queue)
                {
                    if (!_messages.TryGetValue(messageId, out var message))
                    {
                        continue;
                    }

                    var peekedMessage = message.GetPeekedMessage();
                    if (peekedMessage is not null)
                    {
                        output.Add(peekedMessage);
                        continue;
                    }

                    if (output.Count >= maxMessagesValue)
                    {
                        break;
                    }
                }

                return new(StorageResultType.Success, output.ToArray());
            }
        }

        public virtual StorageResultType DeleteMessage(string messageId, string popReceipt)
        {
            lock (_lock)
            {
                if (!_exists)
                {
                    return StorageResultType.DoesNotExist;
                }

                if (!_exists
                    || !_messages.TryGetValue(messageId, out var message)
                    || !message.Delete(popReceipt))
                {
                    return StorageResultType.DoesNotExist;
                }

                if (!_messages.TryRemove(messageId, out var deleted))
                {
                    throw new InvalidOperationException();
                }

                return StorageResultType.Success;
            }
        }

        public virtual StorageResultType Delete()
        {
            lock (_lock)
            {
                if (!_exists)
                {
                    return StorageResultType.DoesNotExist;
                }

                _messages.Clear();
                _queue.Clear();
                _exists = false;
                return StorageResultType.Success;
            }
        }

        public virtual StorageResult<QueueProperties> GetProperties()
        {
            lock (_lock)
            {
                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                return new(StorageResultType.Success, QueuesModelFactory.QueueProperties(
                    metadata: null,
                    approximateMessagesCount: _messages.Count));
            }
        }

        public virtual StorageResult<UpdateReceipt> UpdateMessage(
            string messageId,
            string popReceipt,
            string? messageText,
            TimeSpan visibilityTimeout)
        {
            lock (_lock)
            {
                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                if (!_exists
                    || !_messages.TryGetValue(messageId, out var message))
                {
                    return new(StorageResultType.DoesNotExist);
                }

                var updateReceipt = message.Update(popReceipt, messageText, visibilityTimeout);
                if (updateReceipt is null)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                return new(StorageResultType.Success, updateReceipt);
            }
        }
    }
}

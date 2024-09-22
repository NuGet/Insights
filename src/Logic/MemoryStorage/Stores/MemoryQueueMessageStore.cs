// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Queues.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public class MemoryQueueMessageStore
    {
        private readonly object _lock = new();

        private readonly string _id;
        private string _popReceipt;
        private string _text;
        private readonly DateTimeOffset _insertionTime;
        private readonly DateTimeOffset _expirationTime;
        private DateTimeOffset _timeNextVisible;
        private int _dequeueCount;
        private bool _deleted;

        public MemoryQueueMessageStore(
            string id,
            string text,
            TimeSpan visibilityTimeout)
        {
            _id = id;
            _text = text;
            _popReceipt = MakePopReciept();
            _insertionTime = DateTimeOffset.UtcNow;
            _expirationTime = DateTimeOffset.MaxValue;
            _timeNextVisible = _insertionTime + visibilityTimeout;
            _dequeueCount = 0;
        }

        private static string MakePopReciept()
        {
            return $"pop-receipt-{Guid.NewGuid()}";
        }

        public DateTimeOffset InsertionTime => _insertionTime;
        public int DequeueCount => _dequeueCount;

        public SendReceipt GetSendReceipt()
        {
            lock (_lock)
            {
                if (_dequeueCount != 0 || _deleted)
                {
                    throw new InvalidOperationException();
                }

                return QueuesModelFactory.SendReceipt(
                    _id,
                    _insertionTime,
                    _expirationTime,
                    _popReceipt,
                    _timeNextVisible);
            }
        }

        public QueueMessage? GetQueueMessage(TimeSpan visibilityTimeout)
        {
            lock (_lock)
            {
                if (_deleted)
                {
                    throw new InvalidOperationException();
                }

                var now = DateTimeOffset.UtcNow;

                if (_timeNextVisible > now)
                {
                    return null;
                }

                _popReceipt = MakePopReciept();
                _timeNextVisible = now + visibilityTimeout;
                _dequeueCount++;

                return QueuesModelFactory.QueueMessage(
                    messageId: _id,
                    popReceipt: _popReceipt,
                    body: new BinaryData(_text),
                    dequeueCount: _dequeueCount,
                    nextVisibleOn: _timeNextVisible,
                    insertedOn: _insertionTime,
                    expiresOn: _expirationTime);
            }
        }

        public PeekedMessage? GetPeekedMessage()
        {
            lock (_lock)
            {
                if (_deleted)
                {
                    throw new InvalidOperationException();
                }

                var now = DateTimeOffset.UtcNow;

                if (_timeNextVisible > now)
                {
                    return null;
                }

                return QueuesModelFactory.PeekedMessage(
                    messageId: _id,
                    message: new BinaryData(_text),
                    dequeueCount: _dequeueCount,
                    insertedOn: _insertionTime,
                    expiresOn: _expirationTime);
            }
        }

        public bool Delete(string popReceipt)
        {
            lock (_lock)
            {
                if (_deleted)
                {
                    throw new InvalidOperationException();
                }

                if (popReceipt != _popReceipt)
                {
                    return false;
                }

                _deleted = true;
                return true;
            }
        }

        public UpdateReceipt? Update(
            string popReceipt,
            string? messageText,
            TimeSpan visibilityTimeout)
        {
            lock (_lock)
            {
                if (_deleted)
                {
                    throw new InvalidOperationException();
                }

                if (popReceipt != _popReceipt)
                {
                    return null;
                }

                if (messageText is not null)
                {
                    _text = messageText;
                }

                _popReceipt = MakePopReciept();
                _text = messageText ?? _text;
                _timeNextVisible = DateTimeOffset.UtcNow + visibilityTimeout;
                return QueuesModelFactory.UpdateReceipt(_popReceipt, _timeNextVisible);
            }
        }
    }
}

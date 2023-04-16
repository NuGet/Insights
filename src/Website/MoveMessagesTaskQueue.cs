// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NuGet.Insights.Worker;

namespace NuGet.Insights.Website
{
    public class MoveMessagesTaskQueue
    {
        private readonly Channel<MoveMessagesTask> _queue;
        private readonly ConcurrentDictionary<MoveMessagesTask, int> _pending;
        private MoveMessagesTask _working;

        public MoveMessagesTaskQueue()
        {
            _queue = Channel.CreateUnbounded<MoveMessagesTask>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });
            _pending = new ConcurrentDictionary<MoveMessagesTask, int>();
        }

        public bool IsScheduled(MoveMessagesTask task)
        {
            return _pending.GetOrAdd(task, 0) > 0;
        }

        public bool IsWorking(MoveMessagesTask task)
        {
            return _working == task;
        }

        public bool TryMarkComplete(MoveMessagesTask task)
        {
            return ReferenceEquals(Interlocked.CompareExchange(ref _working, null, task), task);
        }

        public async ValueTask EnqueueAsync(MoveMessagesTask task)
        {
            _pending.AddOrUpdate(task, 1, (_, x) => x + 1);
            await _queue.Writer.WriteAsync(task);
        }

        public async ValueTask<MoveMessagesTask> DequeueAsync(CancellationToken token)
        {
            if (_working is not null)
            {
                throw new InvalidOperationException("The last task should have been marked as complete.");
            }

            var task = await _queue.Reader.ReadAsync(token);

            if (Interlocked.CompareExchange(ref _working, task, null) is not null)
            {
                throw new InvalidOperationException("There appears to be another thread reading from the queue.");
            }

            _pending.AddOrUpdate(task, 0, (_, x) => Math.Max(0, x - 1));
            return task;
        }
    }

    public record MoveMessagesTask(QueueType Source, bool IsPoisonSource, QueueType Destination, bool IsPoisonDestination);
}

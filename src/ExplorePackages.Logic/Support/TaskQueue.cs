using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class TaskQueue<T>
    {
        private readonly object _lock = new object();
        private readonly Queue<Work> _workQueue = new Queue<Work>();
        private readonly int _workerCount;
        private readonly Func<T, Task> _workAsync;
        private readonly ILogger _logger;
        private readonly TaskCompletionSource<Task> _failureTcs;
        private IReadOnlyList<Task> _consumers;

        public TaskQueue(int workerCount, Func<T, Task> workAsync, ILogger logger)
        {
            _workerCount = workerCount;
            _workAsync = workAsync;
            _logger = logger;
            _failureTcs = new TaskCompletionSource<Task>();
        }

        public int Count => _workQueue.Count;

        private Task<Task> FailureTask => _failureTcs.Task;

        public void Start()
        {
            lock (_lock)
            {
                if (_consumers != null)
                {
                    throw new InvalidOperationException("The task queue has already started.");
                }

                _consumers = Enumerable
                    .Range(0, _workerCount)
                    .Select(x => ConsumeAsync())
                    .ToList();
            }
        }

        public async Task ProduceThenCompleteAsync(Func<Task> getProduceTask)
        {
            var failureTask = FailureTask;
            var produceThenCompleteTask = ProduceThenCompleteInternalAsync(getProduceTask);
            var firstTask = await Task.WhenAny(failureTask, produceThenCompleteTask);
            if (firstTask == failureTask)
            {
                await await failureTask;
                throw new InvalidOperationException("The task queue has failed.");
            }
            else
            {
                await produceThenCompleteTask;
            }
        }

        private async Task ProduceThenCompleteInternalAsync(Func<Task> getProduceTask)
        {
            await Task.Yield();
            await getProduceTask();
            await CompleteAsync();
        }

        private async Task CompleteAsync()
        {
            var consumers = _consumers;

            lock (_lock)
            {
                if (_consumers == null)
                {
                    throw new InvalidOperationException("The task queue has not been started.");
                }
                
                for (var i = 0; i < _consumers.Count; i++)
                {
                    Enqueue(new Work(default(T), complete: true));
                }

                _consumers = null;
            }

            await Task.WhenAll(consumers);
        }

        public void Enqueue(T work)
        {
            Enqueue(new Work(work, complete: false));
        }

        private void Enqueue(Work work)
        {
            lock (_lock)
            {
                _workQueue.Enqueue(work);
                Monitor.PulseAll(_lock);
            }
        }

        private async Task ConsumeAsync()
        {
            await Task.Yield();

            while (true)
            {
                Work work;
                lock (_lock)
                {
                    while (_workQueue.Count == 0)
                    {
                        Monitor.Wait(_lock);
                    }

                    work = _workQueue.Dequeue();
                }

                if (work.Complete)
                {
                    return;
                };

                var workTask = _workAsync(work.Data);
                try
                {
                    await workTask;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "A worker in the task queue encountered an exception.");
                    _failureTcs.TrySetResult(workTask);
                    throw;
                }
            }
        }

        private class Work
        {
            public Work(T data, bool complete)
            {
                Data = data;
                Complete = complete;
            }

            public T Data { get; }
            public bool Complete { get; }
        }
    }
}

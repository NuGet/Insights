using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Support
{
    public class TaskQueue<T>
    {
        private readonly object _lock = new object();
        private readonly Queue<Work> _workQueue = new Queue<Work>();
        private readonly int _workerCount;
        private readonly Func<T, Task> _workAsync;
        private IReadOnlyList<Task> _consumers;

        public TaskQueue(int workerCount, Func<T, Task> workAsync)
        {
            _workerCount = workerCount;
            _workAsync = workAsync;
        }

        public int Count => _workQueue.Count;

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

        public async Task CompleteAsync()
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

                await _workAsync(work.Data);
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

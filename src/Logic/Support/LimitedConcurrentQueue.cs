// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;

namespace NuGet.Insights
{
    public class LimitedConcurrentQueue<T> : IReadOnlyCollection<T>
    {
        private bool _hitLimit;
        private readonly object _lock = new();
        private readonly ConcurrentQueue<T> _data;

        public LimitedConcurrentQueue(int limit)
        {
            Limit = limit;
            _hitLimit = false;
            _data = new();
        }

        public int Count => _data.Count;
        public int Limit { get; set; }

        public void Enqueue(T item, Action<int> onHitLimit)
        {
            _data.Enqueue(item);
            var limit = Limit;
            if (_data.Count > limit)
            {
                lock (_lock)
                {
                    if (!_hitLimit)
                    {
                        _hitLimit = true;
                        onHitLimit(limit);
                    }

                    while (_data.Count > limit)
                    {
                        _data.TryDequeue(out _);
                    }
                }
            }
        }

        public void Clear()
        {
            _data.Clear();
            _hitLimit = false;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

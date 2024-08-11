// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.BuildVersionSet
{
    public static class EntityHandle
    {
        public static EntityHandle<T> Create<T>(T value)
        {
            return Create(new EntityReferenceCounter<T>(), value);
        }

        internal static EntityHandle<T> Create<T>(EntityReferenceCounter counter, T value)
        {
            return new EntityHandle<T>(counter, value);
        }
    }

    public class EntityHandle<T> : IDisposable
    {
        private EntityReferenceCounter _counter;
        private T _value;
        private int _disposed;

        internal EntityHandle(EntityReferenceCounter counter, T value)
        {
            _counter = counter;
            _value = value;
            Interlocked.Increment(ref counter.Count);
        }

        public T Value
        {
            get
            {
                if (Disposed)
                {
                    throw new ObjectDisposedException(typeof(EntityHandle<T>).FullName);
                }

                return _value;
            }
        }

        public bool Disposed => _disposed > 0;

        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }

            if (Interlocked.Increment(ref _disposed) == 1)
            {
                if (Interlocked.Decrement(ref _counter.Count) == 0)
                {
                    _counter.Clear();
                }

                _counter = default;
                _value = default;
            }
        }
    }
}

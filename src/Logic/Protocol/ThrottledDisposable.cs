// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class ThrottledDisposable : IDisposable
    {
        private readonly IDisposable? _disposable;
        private readonly IThrottle _throttle;
        private int _disposed = 0;

        public ThrottledDisposable(IDisposable? disposable, IThrottle throttle)
        {
            _disposable = disposable;
            _throttle = throttle;
        }

        public void Dispose()
        {
            _disposable?.Dispose();
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                _throttle.Release();
            }
        }
    }
}

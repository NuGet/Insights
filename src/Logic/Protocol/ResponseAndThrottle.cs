// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class ResponseAndThrottle : IDisposable
    {
        private readonly HttpResponseMessage _response;
        private readonly IThrottle _throttle;
        private int _disposed = 0;

        public ResponseAndThrottle(HttpResponseMessage response, IThrottle throttle)
        {
            _response = response;
            _throttle = throttle;
        }

        public void Dispose()
        {
            _response.Dispose();
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                _throttle.Release();
            }
        }
    }
}

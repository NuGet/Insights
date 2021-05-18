// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class SemaphoreSlimThrottle : IThrottle
    {
        private readonly SemaphoreSlim _semaphoreSlim;

        public SemaphoreSlimThrottle(SemaphoreSlim semaphoreSlim)
        {
            _semaphoreSlim = semaphoreSlim;
        }

        public Task WaitAsync()
        {
            return _semaphoreSlim.WaitAsync();
        }

        public void Release()
        {
            _semaphoreSlim.Release();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class NullAsyncDisposable : IAsyncDisposable
    {
        public static NullAsyncDisposable Instance { get; } = new NullAsyncDisposable();

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}

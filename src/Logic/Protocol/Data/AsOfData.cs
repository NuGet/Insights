// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class AsOfData<T> : IAsyncDisposable, IAsOfData
    {
        public AsOfData(DateTimeOffset asOfTimestamp, string url, string etag, IAsyncEnumerable<T> data)
        {
            AsOfTimestamp = asOfTimestamp;
            Url = url;
            ETag = etag;
            Entries = data;
        }

        public DateTimeOffset AsOfTimestamp { get; }
        public string Url { get; }
        public string ETag { get; }
        public IAsyncEnumerable<T> Entries { get; }

        public ValueTask DisposeAsync()
        {
            return Entries?.GetAsyncEnumerator().DisposeAsync() ?? default;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class AsOfData<T> : IAsyncDisposable, IAsOfData
    {
        public const int DefaultPageSize = 10000;

        public AsOfData(DateTimeOffset asOfTimestamp, Uri url, string etag, IAsyncEnumerable<IReadOnlyList<T>> pages)
        {
            AsOfTimestamp = asOfTimestamp;
            Url = url;
            ETag = etag;
            Pages = pages;
        }

        public DateTimeOffset AsOfTimestamp { get; }
        public Uri Url { get; }
        public string ETag { get; }
        public IAsyncEnumerable<IReadOnlyList<T>> Pages { get; }

        public ValueTask DisposeAsync()
        {
            return Pages?.GetAsyncEnumerator().DisposeAsync() ?? default;
        }
    }
}

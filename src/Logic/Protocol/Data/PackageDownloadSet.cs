// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class PackageDownloadSet : IAsyncDisposable, IAsOfData
    {
        public PackageDownloadSet(DateTimeOffset asOfTimestamp, string url, string etag, IAsyncEnumerable<PackageDownloads> downloads)
        {
            AsOfTimestamp = asOfTimestamp;
            Url = url;
            ETag = etag;
            Entries = downloads;
        }

        public DateTimeOffset AsOfTimestamp { get; }
        public string Url { get; }
        public string ETag { get; }
        public IAsyncEnumerable<PackageDownloads> Entries { get; }

        public ValueTask DisposeAsync()
        {
            return Entries?.GetAsyncEnumerator().DisposeAsync() ?? default;
        }
    }
}

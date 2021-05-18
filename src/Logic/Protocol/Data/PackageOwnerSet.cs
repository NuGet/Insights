// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class PackageOwnerSet : IAsyncDisposable, IAsOfData
    {
        public PackageOwnerSet(DateTimeOffset asOfTimestamp, string url, string etag, IAsyncEnumerable<PackageOwner> owners)
        {
            AsOfTimestamp = asOfTimestamp;
            Url = url;
            ETag = etag;
            Owners = owners;
        }

        public DateTimeOffset AsOfTimestamp { get; }
        public string Url { get; }
        public string ETag { get; }
        public IAsyncEnumerable<PackageOwner> Owners { get; }

        public ValueTask DisposeAsync()
        {
            return Owners?.GetAsyncEnumerator().DisposeAsync() ?? default;
        }
    }
}

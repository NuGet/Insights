// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker
{
    public interface ILatestPackageLeaf : ITableEntityWithClientRequestId
    {
        string PackageId { get; }
        string PackageVersion { get; }
        DateTimeOffset CommitTimestamp { get; }
    }
}

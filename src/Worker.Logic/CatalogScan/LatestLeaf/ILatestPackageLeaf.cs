// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Data.Tables;

namespace NuGet.Insights.Worker
{
    public interface ILatestPackageLeaf : ITableEntity
    {
        string PackageId { get; }
        string PackageVersion { get; }
        DateTimeOffset CommitTimestamp { get; }
    }
}

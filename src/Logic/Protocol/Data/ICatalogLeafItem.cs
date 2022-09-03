// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights
{
    public interface ICatalogLeafItem : IPackageIdentityCommit
    {
        string Url { get; }
        string CommitId { get; }
        CatalogLeafType Type { get; }
        public new DateTimeOffset CommitTimestamp { get; }
    }
}

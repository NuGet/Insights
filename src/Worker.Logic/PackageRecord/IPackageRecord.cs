// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.Worker
{
    public interface IPackageRecord : IPackageCommitRecord
    {
        Guid? ScanId { get; set; }

        DateTimeOffset? ScanTimestamp { get; set; }

        string LowerId { get; set; }

        string Identity { get; set; }

        DateTimeOffset? Created { get; set; }
    }
}

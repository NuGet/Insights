// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.Worker
{
    public interface IPackageRecord
    {
        Guid? ScanId { get; set; }

        DateTimeOffset? ScanTimestamp { get; set; }

        string LowerId { get; set; }

        string Identity { get; set; }

        string Id { get; set; }

        string Version { get; set; }

        DateTimeOffset CatalogCommitTimestamp { get; set; }

        DateTimeOffset? Created { get; set; }
    }
}

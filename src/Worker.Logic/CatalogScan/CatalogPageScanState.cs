// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public enum CatalogPageScanState
    {
        Created, // Initial state before any work is done
        Expanding, // Expanding child entities in storage
        Enqueuing, // Enqueueing messages for child entities
        Complete, // The scan is complete, no more work is required
    }
}

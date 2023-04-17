// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public enum CatalogIndexScanState
    {
        Created, // Initial state before any work is done
        Initialized, // The driver has been initialized
        FindingLatest, // Waiting on the "find latest leaves" scan
        StartingExpand, // Starting a custom, driver-provided expand flow
        Expanding, // Expanding child entities in storage
        Enqueuing, // Enqueueing messages for child entities
        Working, // Waiting for child entities to be completed
        StartingAggregate, // Starting the aggregating processes
        Aggregating, // Waiting for the aggregation to complete
        Finalizing, // Finalize or clean up from the scan
        Aborted, // The scan was aborted before completed and will no longer be processed
        Complete, // The scan is complete, no more work is required
    }

    public static class CatalogIndexScanStateExtensions
    {
        public static bool IsTerminal(this CatalogIndexScanState state)
        {
            return state == CatalogIndexScanState.Complete || state == CatalogIndexScanState.Aborted;
        }
    }
}

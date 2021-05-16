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
        Complete, // The scan is complete, no more work is required
    }
}

namespace Knapcode.ExplorePackages.Worker
{
    public enum CatalogPageScanState
    {
        Created, // Initial state before any work is done
        Expanding, // Expanding child entities in storage
        Enqueuing, // Enqueueing messages for child entities
        Complete, // The scan is complete, no more work is required
    }
}

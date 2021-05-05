namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public enum KustoContainerIngestionState
    {
        Created,
        CreatingTable,
        Expanding,
        Enqueuing,
        Working,
        SwappingTable,
        DroppingOldTable,
    }
}

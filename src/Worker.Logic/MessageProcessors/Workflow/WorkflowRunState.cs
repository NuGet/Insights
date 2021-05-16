namespace Knapcode.ExplorePackages.Worker.Workflow
{
    public enum WorkflowRunState
    {
        Created,
        CatalogScanWorking,
        AuxiliaryFilesWorking,
        KustoIngestionWorking,
        Complete,
    }
}

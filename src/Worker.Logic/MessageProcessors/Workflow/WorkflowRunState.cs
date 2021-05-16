namespace NuGet.Insights.Worker.Workflow
{
    public enum WorkflowRunState
    {
        Created,
        CatalogScanWorking,
        AuxiliaryFilesWorking,
        KustoIngestionWorking,
        Finalizing,
        Complete,
    }
}

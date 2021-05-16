using System;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public interface ICsvResultStorage
    {
        /// <summary>
        /// The Azure Blob Storage container name to write CSV results to.
        /// </summary>
        string ContainerName { get; }

        Type RecordType { get; }

        string BlobNamePrefix { get; }
    }
}

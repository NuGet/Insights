using System;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public record CsvResultStorage : ICsvResultStorage
    {
        public CsvResultStorage(string containerName, Type recordType, string blobNamePrefix)
        {
            ContainerName = containerName;
            RecordType = recordType;
            BlobNamePrefix = blobNamePrefix;
        }

        public string ContainerName { get; }
        public Type RecordType { get; }
        public string BlobNamePrefix { get; }
    }
}

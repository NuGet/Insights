namespace NuGet.Insights.Worker.KustoIngestion
{
    public class CsvResultBlob
    {
        public CsvResultBlob(string name, long rawSizeBytes)
        {
            Name = name;
            RawSizeBytes = rawSizeBytes;
        }

        public string Name { get; }
        public long RawSizeBytes { get; }
    }
}

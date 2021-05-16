using System;

namespace NuGet.Insights.Worker.CatalogLeafItemToCsv
{
    public partial record CatalogLeafItemRecord : ICsvRecord
    {
        public string CommitId { get; set; }
        public DateTimeOffset CommitTimestamp { get; set; }
        public string LowerId { get; set; }

        [KustoPartitionKey]
        public string Identity { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }
        public CatalogLeafType Type { get; set; }
        public string Url { get; set; }

        public string PageUrl { get; set; }
    }
}

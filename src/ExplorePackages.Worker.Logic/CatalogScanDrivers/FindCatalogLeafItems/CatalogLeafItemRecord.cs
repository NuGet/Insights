using System;

namespace Knapcode.ExplorePackages.Worker.FindCatalogLeafItems
{
    public partial record CatalogLeafItemRecord : ICsvRecord<CatalogLeafItemRecord>
    {
        public string CommitId { get; set; }
        public DateTimeOffset CommitTimestamp { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
        public CatalogLeafType Type { get; set; }
        public string Url { get; set; }

        public string LowerId { get; set; }
        public string LowerNormalizedVersion { get; set; }

        public string PageUrl { get; set; }
    }
}

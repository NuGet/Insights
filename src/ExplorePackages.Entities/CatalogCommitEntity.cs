using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities
{
    public class CatalogCommitEntity
    {
        public long CatalogPageKey { get; set; }
        public long CatalogCommitKey { get; set; }
        public string CommitId { get; set; }
        public long CommitTimestamp { get; set; }
        public int Count { get; set; }

        public CatalogPageEntity CatalogPage { get; set; }
        public List<CatalogLeafEntity> CatalogLeaves { get; set; }
    }
}

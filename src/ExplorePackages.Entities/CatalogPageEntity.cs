using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities
{
    public class CatalogPageEntity
    {
        public long CatalogPageKey { get; set; }
        public string Url { get; set; }

        public List<CatalogCommitEntity> CatalogCommits { get; set; }
    }
}

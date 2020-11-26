using System;

namespace Knapcode.ExplorePackages.Logic
{
    public interface ICatalogLeafItem
    {
        string CommitId { get; }
        DateTimeOffset CommitTimestamp { get; }
        string PackageId { get; }
        string PackageVersion { get; }
        CatalogLeafType Type { get; }
    }
}

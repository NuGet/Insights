using System;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public interface ICatalogLeafItem
    {
        DateTimeOffset CommitTimestamp { get; }
        string PackageId { get; }
        string PackageVersion { get; }
        CatalogLeafType Type { get; }
    }
}

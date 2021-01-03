using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ILatestPackageLeaf : ITableEntity
    {
        string PackageId { get; }
        string PackageVersion { get; }
        DateTimeOffset CommitTimestamp { get; }
    }
}

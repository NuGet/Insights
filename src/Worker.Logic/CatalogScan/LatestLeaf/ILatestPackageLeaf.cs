using System;
using Azure.Data.Tables;

namespace NuGet.Insights.Worker
{
    public interface ILatestPackageLeaf : ITableEntity
    {
        string PackageId { get; }
        string PackageVersion { get; }
        DateTimeOffset CommitTimestamp { get; }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public delegate List<T> Prune<T>(List<T> records, bool isFinalPrune) where T : ICsvRecord;

    public delegate string GetBucketKey<T>(T record) where T : ICsvRecord;

    public interface ICsvResultStorage<T> where T : IAggregatedCsvRecord<T>
    {
        /// <summary>
        /// The Azure Blob Storage container name to write CSV results to.
        /// </summary>
        string ResultContainerName { get; }
    }
}

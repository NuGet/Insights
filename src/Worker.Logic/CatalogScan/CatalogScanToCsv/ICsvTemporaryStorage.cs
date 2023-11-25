// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public interface ICsvTemporaryStorage
    {
        Task AppendAsync<T>(string storageSuffix, IReadOnlyList<ICsvRecordSet<T>> sets) where T : class, ICsvRecord;
        Task FinalizeAsync(string storageSuffix);
        Task InitializeAsync(string storageSuffix);
        Task<bool> IsAggregateCompleteAsync(string aggregatePartitionKeyPrefix, string storageSuffix);
        Task StartAggregateAsync(string aggregatePartitionKeyPrefix, string storageSuffix);
    }
}

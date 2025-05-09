// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.BuildVersionSet;

namespace NuGet.Insights.Worker.AuxiliaryFileUpdater
{
    public interface IAuxiliaryFileUpdater
    {
        string OperationName { get; }
        string Title { get; }
        string ContainerName { get; }
        bool HasRequiredConfiguration { get; }
        bool AutoStart { get; }
        TimerFrequency Frequency { get; }
    }

    public interface IAuxiliaryFileUpdater<TInput, TRecord> : IAuxiliaryFileUpdater
        where TInput : IAsOfData
        where TRecord : IAuxiliaryFileCsvRecord<TRecord>
    {
        Task<TInput> GetDataAsync();
        IAsyncEnumerable<IReadOnlyList<TRecord>> ProduceRecordsAsync(IVersionSet versionSet, TInput data);
    }
}

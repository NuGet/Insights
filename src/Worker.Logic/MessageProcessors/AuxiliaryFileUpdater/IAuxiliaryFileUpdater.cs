// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.BuildVersionSet;

namespace NuGet.Insights.Worker.AuxiliaryFileUpdater
{
    public interface IAuxiliaryFileUpdater
    {
        string OperationName { get; }
        string BlobName { get; }
        string ContainerName { get; }
        Type RecordType { get; }
        bool HasRequiredConfiguration { get; }
        bool AutoStart { get; }
        TimeSpan Frequency { get; }
    }

    public interface IAuxiliaryFileUpdater<T> : IAuxiliaryFileUpdater where T : IAsOfData
    {
        Task<T> GetDataAsync();
        Task WriteAsync(IVersionSet versionSet, T data, TextWriter writer);
    }
}

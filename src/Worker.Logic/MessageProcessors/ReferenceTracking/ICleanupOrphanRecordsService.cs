// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public interface ICleanupOrphanRecordsService<T> : ICleanupOrphanRecordsService where T : ICsvRecord
    {
    }

    public interface ICleanupOrphanRecordsService
    {
        Task InitializeAsync();
        Task<bool> IsRunningAsync();
        Task<bool> StartAsync();
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public interface IRemoteCursorClient
    {
        Task<DateTimeOffset> GetCatalogAsync(CancellationToken token = default);
        Task<DateTimeOffset> GetFlatContainerAsync(CancellationToken token = default);
        Task<DateTimeOffset> GetRegistrationAsync(CancellationToken token = default);
        Task<DateTimeOffset> GetSearchAsync();
    }
}
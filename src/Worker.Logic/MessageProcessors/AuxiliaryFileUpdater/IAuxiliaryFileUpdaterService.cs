// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.AuxiliaryFileUpdater
{
    public interface IAuxiliaryFileUpdaterService<T> : IAuxiliaryFileUpdaterService
    {
    }

    public interface IAuxiliaryFileUpdaterService
    {
        Task InitializeAsync();
        Task DestroyAsync();
        Task<bool> StartAsync();
        Task<bool> IsRunningAsync();
        bool HasRequiredConfiguration { get; }
    }
}

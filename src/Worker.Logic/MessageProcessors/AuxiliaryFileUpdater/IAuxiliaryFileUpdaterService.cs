// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Insights.Worker.AuxiliaryFileUpdater
{
    public interface IAuxiliaryFileUpdaterService<T>
    {
        Task InitializeAsync();
        Task<bool> StartAsync();
        Task<bool> IsRunningAsync();
        bool HasRequiredConfiguration { get; }
    }
}

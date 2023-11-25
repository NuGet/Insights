// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public interface ITimer
    {
        string Name { get; }
        TimeSpan Frequency { get; }
        bool AutoStart { get; }
        bool IsEnabled { get; }
        bool CanAbort { get; }
        bool CanDestroy { get; }

        Task<bool> IsRunningAsync();
        Task InitializeAsync();
        Task DestroyAsync();

        /// <summary>
        /// Returns true if the logic was completed or at least queued successfully. False, otherwise.
        /// </summary>
        Task<bool> ExecuteAsync();

        Task AbortAsync();
    }
}

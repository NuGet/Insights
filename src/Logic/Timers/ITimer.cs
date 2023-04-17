// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public interface ITimer
    {
        string Name { get; }
        TimeSpan Frequency { get; }
        bool AutoStart { get; }
        bool IsEnabled { get; }
        bool CanAbort { get; }

        /// <summary>
        /// Use to group timers that can be run in parallel. Return 0 (<c>default</c>) for simple, parallel execution.
        /// </summary>
        int Order { get; }

        Task<bool> IsRunningAsync();
        Task InitializeAsync();

        /// <summary>
        /// Returns true if the logic was completed or at least queued successfully. False, otherwise.
        /// </summary>
        Task<bool> ExecuteAsync();

        Task AbortAsync();
    }
}

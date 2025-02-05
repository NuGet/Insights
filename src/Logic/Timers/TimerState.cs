// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public record TimerState
    {
        /// <summary>
        /// "PascalCase" identifier for the timer, useful for HTML attributes or logs, e.g. "DownloadsToCsv".
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Used for display purposes on the admin panel, e.g. "Downloads to CSV".
        /// </summary>
        public required string Title { get; init; }

        public required bool IsEnabledInStorage { get; init; }
        public required bool IsEnabledInConfig { get; init; }
        public required bool IsRunning { get; init; }
        public required bool CanAbort { get; init; }
        public required bool CanDestroy { get; init; }
        public required DateTimeOffset? LastExecuted { get; init; }
        public required TimeSpan Frequency { get; init; }
        public required DateTimeOffset? NextRun { get; init; }
    }
}

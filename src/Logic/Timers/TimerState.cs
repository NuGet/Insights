// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights
{
    public record TimerState
    {
        public string Name { get; init; }
        public bool? IsEnabledInStorage { get; init; }
        public bool IsEnabledInConfig { get; init; }
        public bool IsRunning { get; init; }
        public bool CanAbort { get; init; }
        public bool CanDestroy { get; init; }
        public DateTimeOffset? LastExecuted { get; init; }
        public TimeSpan Frequency { get; init; }
    }
}

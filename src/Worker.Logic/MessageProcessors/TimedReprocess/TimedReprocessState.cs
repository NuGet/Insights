// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.TimedReprocess
{
    public enum TimedReprocessState
    {
        Created,
        Working,
        Finalizing,
        Aborted,
        Complete,
    }


    public static class TimedReprocessStateExtensions
    {
        public static bool IsTerminal(this TimedReprocessState state)
        {
            return state == TimedReprocessState.Aborted
                || state == TimedReprocessState.Complete;
        }
    }
}

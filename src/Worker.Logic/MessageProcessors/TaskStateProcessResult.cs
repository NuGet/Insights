// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public enum TaskStateProcessResult
    {
        /// <summary>
        /// The work is done for this task. The message should be marked as completed.
        /// </summary>
        Complete,

        /// <summary>
        /// The work is not done and trying again should be delayed. The message should be requeued with a higher delay.
        /// </summary>
        Delay,

        /// <summary>
        /// The work is not done and trying again should happen soon. The message should be requeued with minimal delay.
        /// </summary>
        Continue,
    }
}

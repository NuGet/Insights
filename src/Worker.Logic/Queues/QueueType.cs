// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public enum QueueType
    {
        /// <summary>
        /// The main queue used for processing units of work. When in doubt, enqueue to this one.
        /// </summary>
        Work,

        /// <summary>
        /// The queue used for expanding messages into more messages, with relatively low effort. Ideally this queue
        /// remains relatively small and workers can pull messages off this queue to fill up the <see cref="Work"/>
        /// queue.
        /// </summary>
        Expand,
    }
}
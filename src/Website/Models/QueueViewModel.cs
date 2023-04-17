// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker;

namespace NuGet.Insights.Website
{
    public class QueueViewModel
    {
        public QueueType QueueType { get; set; }
        public int ApproximateMessageCount { get; set; }
        public int AvailableMessageCountLowerBound { get; set; }
        public bool AvailableMessageCountIsExact { get; set; }
        public int PoisonApproximateMessageCount { get; set; }
        public int PoisonAvailableMessageCountLowerBound { get; set; }
        public bool PoisonAvailableMessageCountIsExact { get; set; }
        public MoveQueueMessagesState MoveMainToPoisonState { get; set; }
        public MoveQueueMessagesState MovePoisonToMainState { get; set; }
    }
}

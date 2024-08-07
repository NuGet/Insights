// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using Azure;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights
{
    public class TimerEntity : ITableEntityWithClientRequestId
    {
        public TimerEntity()
        {
        }

        public TimerEntity(string name)
        {
            PartitionKey = TimerExecutionService.PartitionKey;
            RowKey = name;
        }

        [IgnoreDataMember]
        public string Name => RowKey;

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public Guid? ClientRequestId { get; set; }

        public DateTimeOffset? LastExecuted { get; set; }
        public bool IsEnabled { get; set; }

        /// <summary>
        /// The time to next run this timer, as an override. This value will override the timer's normal schedule only
        /// once, and then will be cleared.
        /// </summary>
        public DateTimeOffset? NextRun { get; set; }
    }
}

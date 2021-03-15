using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages
{
    public class TimerEntity : TableEntity
    {
        public TimerEntity()
        {
        }

        public TimerEntity(string name)
        {
            PartitionKey = TimerExecutionService.PartitionKey;
            RowKey = name;
        }

        public DateTimeOffset? LastExecuted { get; set; }
        public bool IsEnabled { get; set; }
    }
}

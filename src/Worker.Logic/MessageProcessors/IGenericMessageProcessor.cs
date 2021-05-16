using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Insights.Worker
{
    public interface IGenericMessageProcessor
    {
        Task ProcessSingleAsync(QueueType queue, string message, long dequeueCount);
        Task ProcessBatchAsync(string schemaName, int schemaVersion, IReadOnlyList<JToken> data, long dequeueCount);
    }
}
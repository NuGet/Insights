using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public interface IGenericMessageProcessor
    {
        Task ProcessAsync(string message, int dequeueCount);
        Task<BatchMessageProcessorResult<JToken>> ProcessAsync(string schemaName, int schemaVersion, IReadOnlyList<JToken> data, int dequeueCount);
    }
}
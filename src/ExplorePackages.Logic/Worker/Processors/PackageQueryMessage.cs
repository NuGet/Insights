using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class PackageQueryMessage
    {
        [JsonConstructor]
        public PackageQueryMessage(long minKey, long maxKey)
        {
            MinKey = minKey;
            MaxKey = maxKey;
        }

        [JsonRequired]
        public long MinKey { get; }

        [JsonRequired]
        public long MaxKey { get; }
    }
}

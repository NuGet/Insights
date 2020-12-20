using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class MessageBatcher
    {
        private static readonly string ThisNamespaceDot = typeof(MessageBatcher).Namespace + ".";
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public MessageBatcher(IOptions<ExplorePackagesWorkerSettings> options)
        {
            _options = options;
        }

        public IReadOnlyList<HomogeneousBatchMessage> BatchOrNull<T>(IReadOnlyList<T> messages, ISchemaSerializer<T> serializer)
        {
            var messageType = typeof(T);
            int batchSize;
            if (messageType == typeof(HomogeneousBatchMessage)
               || messages.Count <= 1
               || _options.Value.MessageBatchSizes == null
               || !TryGetBatchSize(messageType, out batchSize)
               || batchSize <= 1)
            {
                return null;
            }

            var batches = new List<HomogeneousBatchMessage>();
            foreach (var message in messages)
            {
                if (batches.Count == 0 || batches.Last().Messages.Count >= batchSize)
                {
                    batches.Add(new HomogeneousBatchMessage
                    {
                        SchemaName = serializer.Name,
                        SchemaVersion = serializer.LatestVersion,
                        Messages = new List<JToken>(),
                    });
                }

                batches.Last().Messages.Add(serializer.SerializeData(message).AsJToken());
            }

            return batches;
        }

        private bool TryGetBatchSize(Type messageType, out int batchSize)
        {
            if (messageType.FullName.StartsWith(ThisNamespaceDot))
            {
                var nameSuffix = messageType.FullName.Substring(ThisNamespaceDot.Length);
                if (_options.Value.MessageBatchSizes.TryGetValue(nameSuffix, out batchSize))
                {
                    return true;
                }
            }
            
            if (_options.Value.MessageBatchSizes.TryGetValue(messageType.FullName, out batchSize))
            {
                return true;
            }

            return false;
        }
    }
}

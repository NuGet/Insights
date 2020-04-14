using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic.Worker;
using Microsoft.Azure.WebJobs;

namespace Knapcode.ExplorePackages.Worker
{
    public class WebJobMessageEnqueuer : IMessageEnqueuer
    {
        private readonly MessageSerializer _messageSerializer;
        private ICollector<byte[]> _collector;

        public WebJobMessageEnqueuer(MessageSerializer messageSerializer)
        {
            _messageSerializer = messageSerializer;
        }

        public async Task EnqueueAsync(IReadOnlyList<CatalogIndexScanMessage> messages)
        {
            await EnqueueAsync(messages, m => _messageSerializer.Serialize(m));
        }

        public async Task EnqueueAsync(IReadOnlyList<CatalogPageScanMessage> messages)
        {
            await EnqueueAsync(messages, m => _messageSerializer.Serialize(m));
        }

        public void SetCollector(ICollector<byte[]> collector)
        {
            var output = Interlocked.CompareExchange(ref _collector, collector, null);
            if (output != null)
            {
                throw new InvalidOperationException("The collector has already been set.");
            }
        }

        private Task EnqueueAsync<T>(IReadOnlyList<T> messages, Func<T, byte[]> serialize)
        {
            if (_collector == null)
            {
                throw new InvalidOperationException("The collector has not been set.");
            }

            foreach (var message in messages)
            {
                var bytes = serialize(message);
                _collector.Add(bytes);
            }

            return Task.CompletedTask;
        }
    }
}

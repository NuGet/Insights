using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.FindPackageAssets;
using Knapcode.ExplorePackages.Worker.RunRealRestore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class MessageEnqueuer
    {
        private readonly SchemaSerializer _serializer;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;
        private readonly ILogger<MessageEnqueuer> _logger;

        public MessageEnqueuer(
            SchemaSerializer serializer,
            IRawMessageEnqueuer rawMessageEnqueuer,
            ILogger<MessageEnqueuer> logger)
        {
            _serializer = serializer;
            _rawMessageEnqueuer = rawMessageEnqueuer;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _rawMessageEnqueuer.InitializeAsync();
        }

        public Task EnqueueAsync(IReadOnlyList<CatalogIndexScanMessage> messages) => EnqueueAsync(messages, TimeSpan.Zero);
        public Task EnqueueAsync(IReadOnlyList<CatalogIndexScanMessage> messages, TimeSpan notBefore) => GetSchemaAndEnqueueAsync(messages, notBefore);

        public Task EnqueueAsync(IReadOnlyList<CatalogPageScanMessage> messages) => EnqueueAsync(messages, TimeSpan.Zero);
        public Task EnqueueAsync(IReadOnlyList<CatalogPageScanMessage> messages, TimeSpan notBefore) => GetSchemaAndEnqueueAsync(messages, notBefore);

        public Task EnqueueAsync(IReadOnlyList<CatalogLeafScanMessage> messages) => EnqueueAsync(messages, TimeSpan.Zero);
        public Task EnqueueAsync(IReadOnlyList<CatalogLeafScanMessage> messages, TimeSpan notBefore) => GetSchemaAndEnqueueAsync(messages, notBefore);

        public Task EnqueueAsync(IReadOnlyList<FindPackageAssetsCompactMessage> messages) => EnqueueAsync(messages, TimeSpan.Zero);
        public Task EnqueueAsync(IReadOnlyList<FindPackageAssetsCompactMessage> messages, TimeSpan notBefore) => GetSchemaAndEnqueueAsync(messages, notBefore);

        public Task EnqueueAsync(IReadOnlyList<RunRealRestoreMessage> messages) => EnqueueAsync(messages, TimeSpan.Zero);
        public Task EnqueueAsync(IReadOnlyList<RunRealRestoreMessage> messages, TimeSpan notBefore) => GetSchemaAndEnqueueAsync(messages, notBefore);

        public Task EnqueueAsync(IReadOnlyList<RunRealRestoreCompactMessage> messages) => EnqueueAsync(messages, TimeSpan.Zero);
        public Task EnqueueAsync(IReadOnlyList<RunRealRestoreCompactMessage> messages, TimeSpan notBefore) => GetSchemaAndEnqueueAsync(messages, notBefore);

        private async Task GetSchemaAndEnqueueAsync<T>(IReadOnlyList<T> messages, TimeSpan notBefore)
        {
            await EnqueueAsync(messages, _serializer.GetSerializer<T>(), notBefore);
        }

        public async Task EnqueueAsync<T>(IReadOnlyList<T> messages, ISchemaSerializer<T> schema)
        {
            await EnqueueAsync(messages, schema, TimeSpan.Zero);
        }

        public async Task EnqueueAsync<T>(IReadOnlyList<T> messages, ISchemaSerializer<T> schema, TimeSpan notBefore)
        {
            if (messages.Count == 0)
            {
                return;
            }

            var bulkEnqueueStrategy = _rawMessageEnqueuer.BulkEnqueueStrategy;
            if (!bulkEnqueueStrategy.IsEnabled || messages.Count < bulkEnqueueStrategy.Threshold)
            {
                var serializedMessages = messages.Select(m => schema.SerializeMessage(m).AsString()).ToList();
                await _rawMessageEnqueuer.AddAsync(serializedMessages, notBefore);
            }
            else
            {
                var batch = new List<JToken>();
                var batchMessage = new HomogeneousBulkEnqueueMessage
                {
                    SchemaName = schema.Name,
                    SchemaVersion = schema.LatestVersion,
                    Messages = batch,
                    NotBefore = notBefore <= TimeSpan.Zero ? (TimeSpan?)null : notBefore,
                };
                var emptyBatchMessageLength = GetMessageLength(batchMessage);
                var batchMessageLength = emptyBatchMessageLength;

                for (int i = 0; i < messages.Count; i++)
                {
                    var innerData = schema.SerializeData(messages[i]);
                    var innerDataLength = GetMessageLength(innerData);

                    if (!batch.Any())
                    {
                        batch.Add(innerData.AsJToken());
                        batchMessageLength += innerDataLength;
                    }
                    else
                    {
                        var newBatchMessageLength = batchMessageLength + ",".Length + innerDataLength;
                        if (newBatchMessageLength > bulkEnqueueStrategy.MaxSize)
                        {
                            await EnqueueBulkEnqueueMessageAsync(batchMessage, batchMessageLength);
                            batch.Clear();
                            batch.Add(innerData.AsJToken());
                            batchMessageLength = emptyBatchMessageLength + innerDataLength;
                        }
                        else
                        {
                            batch.Add(innerData.AsJToken());
                            batchMessageLength = newBatchMessageLength;
                        }
                    }
                }

                if (batch.Count > 0)
                {
                    await EnqueueBulkEnqueueMessageAsync(batchMessage, batchMessageLength);
                }
            }
        }

        private async Task EnqueueBulkEnqueueMessageAsync(HomogeneousBulkEnqueueMessage batchMessage, int expectedLength)
        {
            var bytes = _serializer.Serialize(batchMessage).AsString();
            if (bytes.Length != expectedLength)
            {
                throw new InvalidOperationException(
                    $"The bulk enqueue message had an unexpected size. " +
                    $"Expected: {expectedLength}. " +
                    $"Actual: {bytes.Length}");
            }

            _logger.LogInformation("Enqueueing a bulk enqueue message containing {Count} individual messages.", batchMessage.Messages.Count);
            await _rawMessageEnqueuer.AddAsync(new[] { bytes });
        }

        private int GetMessageLength(HomogeneousBulkEnqueueMessage batchMessage)
        {
            return GetMessageLength(_serializer.Serialize(batchMessage));
        }

        private static int GetMessageLength(ISerializedEntity innerMessage)
        {
            return Encoding.UTF8.GetByteCount(innerMessage.AsString());
        }
    }
}

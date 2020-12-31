using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class GenericMessageProcessor
    {
        private readonly SchemaSerializer _serializer;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<GenericMessageProcessor> _logger;

        public GenericMessageProcessor(
            SchemaSerializer serializer,
            IServiceProvider serviceProvider,
            ITelemetryClient telemetryClient,
            ILogger<GenericMessageProcessor> logger)
        {
            _serializer = serializer;
            _serviceProvider = serviceProvider;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task ProcessAsync(string message, int dequeueCount)
        {
            using (_logger.BeginScope("Processing string message {Scope_TruncatedMessage} with dequeue count {Scope_DequeueCount}", TruncateMessage(message), dequeueCount))
            {
                NameVersionMessage<object> deserializedMessage;
                try
                {
                    deserializedMessage = _serializer.Deserialize(message);
                }
                catch (JsonException)
                {
                    message = Encoding.UTF8.GetString(Convert.FromBase64String(message));
                    deserializedMessage = _serializer.Deserialize(message);
                }

                await ProcessAsync(deserializedMessage, dequeueCount);
            }
        }

        public async Task ProcessAsync(NameVersionMessage<JToken> message, int dequeueCount)
        {
            using (_logger.BeginScope("Processing deserialized message {TruncatedMessage} with dequeue count {DequeueCount}", TruncateMessage(message), dequeueCount))
            {
                var deserializedMessage = _serializer.Deserialize(message);

                await ProcessAsync(deserializedMessage, dequeueCount);
            }
        }

        private async Task ProcessAsync(NameVersionMessage<object> deserializedMessage, int dequeueCount)
        {
            var messageType = deserializedMessage.Data.GetType();
            var processorType = typeof(IMessageProcessor<>).MakeGenericType(deserializedMessage.Data.GetType());
            var processor = _serviceProvider.GetService(processorType);

            if (processor == null)
            {
                throw new NotSupportedException(
                    $"The message type '{deserializedMessage.Data.GetType().FullName}', " +
                    $"schema name '{deserializedMessage.SchemaName}', " +
                    $"schema version {deserializedMessage.SchemaVersion} is not supported.");
            }

            var method = processorType.GetMethod(nameof(IMessageProcessor<object>.ProcessAsync), new Type[] { messageType, typeof(int) });

            var stopwatch = Stopwatch.StartNew();
            var success = false;
            try
            {
                await (Task)method.Invoke(processor, new object[] { deserializedMessage.Data, dequeueCount });
                success = true;
            }
            finally
            {
                var metric = _telemetryClient.GetMetric($"MessageProcessor{(success ? "Success" : "Failure")}DurationMs", "SchemaName");
                metric.TrackValue(stopwatch.Elapsed.TotalMilliseconds, deserializedMessage.SchemaName);
            }
        }

        private static string TruncateMessage(NameVersionMessage<JToken> message)
        {
            return TruncateMessage(NameVersionSerializer.SerializeData(message).AsString());
        }

        private static string TruncateMessage(string message)
        {
            const int trucationLength = 256;
            var truncatedMessage = message;
            if (message.Length > trucationLength)
            {
                truncatedMessage = message.Substring(0, trucationLength) + "...";
            }

            return truncatedMessage;
        }
    }
}

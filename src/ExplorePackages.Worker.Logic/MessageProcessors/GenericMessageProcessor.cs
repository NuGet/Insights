using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class GenericMessageProcessor
    {
        private readonly SchemaSerializer _serializer;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITelemetryClient _telemetryClient;

        public GenericMessageProcessor(
            SchemaSerializer serializer,
            IServiceProvider serviceProvider,
            ITelemetryClient telemetryClient)
        {
            _serializer = serializer;
            _serviceProvider = serviceProvider;
            _telemetryClient = telemetryClient;
        }

        public async Task ProcessAsync(string message, int dequeueCount)
        {
            object deserializedMessage;
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

        public async Task ProcessAsync(NameVersionMessage<JToken> message, int dequeueCount)
        {
            var deserializedMessage = _serializer.Deserialize(message);

            await ProcessAsync(deserializedMessage, dequeueCount);
        }

        private async Task ProcessAsync(object deserializedMessage, int dequeueCount)
        {
            var messageType = deserializedMessage.GetType();
            var processorType = typeof(IMessageProcessor<>).MakeGenericType(deserializedMessage.GetType());
            var processor = _serviceProvider.GetService(processorType);

            if (processor == null)
            {
                throw new NotSupportedException($"The message type '{deserializedMessage.GetType().FullName}' is not supported.");
            }

            var method = processorType.GetMethod(nameof(IMessageProcessor<object>.ProcessAsync), new Type[] { messageType, typeof(int) });

            var stopwatch = Stopwatch.StartNew();
            var success = false;
            try
            {
                await (Task)method.Invoke(processor, new object[] { deserializedMessage, dequeueCount });
                success = true;
            }
            finally
            {
                var metric = _telemetryClient.GetMetric($"MessageProcessor{(success ? "Success" : "Failure")}DurationMs", "TypeName");
                metric.TrackValue(stopwatch.ElapsedMilliseconds, processor.GetType().FullName);
            }
        }
    }
}

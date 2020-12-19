using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

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

        public async Task ProcessAsync(string message)
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

            await ProcessAsync(deserializedMessage);
        }

        public async Task ProcessAsync(NameVersionMessage<JToken> message)
        {
            var deserializedMessage = _serializer.Deserialize(message);

            await ProcessAsync(deserializedMessage);
        }

        private async Task ProcessAsync(object deserializedMessage)
        {
            var messageType = deserializedMessage.GetType();
            var processorType = typeof(IMessageProcessor<>).MakeGenericType(deserializedMessage.GetType());
            var processor = _serviceProvider.GetService(processorType);

            if (processor == null)
            {
                throw new NotSupportedException($"The message type '{deserializedMessage.GetType().FullName}' is not supported.");
            }

            var method = processorType.GetMethod(nameof(IMessageProcessor<object>.ProcessAsync), new Type[] { messageType });

            var stopwatch = Stopwatch.StartNew();
            var success = false;
            try
            {
                await (Task)method.Invoke(processor, new object[] { deserializedMessage });
                success = true;
            }
            finally
            {
                /*
                var metric = _telemetryClient.GetMetric("MessageProcessorDurationMs", "TypeName", "Success");
                metric.TrackValue(stopwatch.ElapsedMilliseconds, processor.GetType().FullName, success ? "true" : "false");
                */
                _telemetryClient.TrackMetric("MessageProcessorDurationMs", stopwatch.ElapsedMilliseconds, new Dictionary<string, string>
                {
                    {"TypeName", processor.GetType().FullName },
                    {"Success", success ? "true" : "false" },
                });
            }
        }
    }
}

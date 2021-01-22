using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

            await ProcessAsync(
                deserializedMessage.SchemaName,
                deserializedMessage.SchemaVersion,
                deserializedMessage.Data.GetType(),
                new[] { deserializedMessage.Data },
                dequeueCount,
                throwOnException: true);
        }

        public async Task<BatchMessageProcessorResult<JToken>> ProcessAsync(string schemaName, int schemaVersion, IReadOnlyList<JToken> data, int dequeueCount)
        {
            var deserializer = _serializer.GetDeserializer(schemaName);

            var batch = data.Select(x => new { JToken = x, Object = deserializer.Deserialize(schemaVersion, x) }).ToList();
            var objectToJToken = batch.ToDictionary(x => x.Object, x => x.JToken, ReferenceEqualityComparer<object>.Instance);

            var result = await ProcessAsync(
                schemaName,
                schemaVersion,
                deserializer.Type,
                batch.Select(x => x.Object).ToList(),
                dequeueCount,
                throwOnException: false);

            return new BatchMessageProcessorResult<JToken>(
                failed: result.Failed.Select(x => objectToJToken[x]).ToList(),
                tryAgainLater: result
                    .TryAgainLater
                    .Select(pair => new
                    {
                        NotBefore = pair.Key,
                        Messages = pair.Value.Select(x => objectToJToken[x]).ToList(),
                    })
                    .ToDictionary(x => x.NotBefore, x => (IReadOnlyList<JToken>)x.Messages));
        }

        private async Task<BatchMessageProcessorResult<object>> ProcessAsync(
            string schemaName,
            int schemaVersion,
            Type messageType,
            IReadOnlyList<object> messages,
            int dequeueCount,
            bool throwOnException)
        {
            var batchProcessorType = typeof(IBatchMessageProcessor<>).MakeGenericType(messageType);
            var batchProcessor = _serviceProvider.GetService(batchProcessorType);

            if (batchProcessor != null)
            {
                var readOnlyListMessageType = typeof(IReadOnlyList<>).MakeGenericType(messageType);
                var processAsyncMethod = batchProcessorType.GetMethod(nameof(IBatchMessageProcessor<object>.ProcessAsync), new Type[] { readOnlyListMessageType, typeof(int) });

                // Make IReadOnlyList<T> instead of IReadOnlyList<object>.
                var stronglyTypedMessages = MakeListOfT(messageType, messages);

                // Execute the batch processor.
                var stopwatch = Stopwatch.StartNew();
                var success = false;
                Task task;
                try
                {
                    task = (Task)processAsyncMethod.Invoke(batchProcessor, new object[] { stronglyTypedMessages, dequeueCount });
                    await task;
                    success = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError("An exception occurred." + Environment.NewLine + "{ExceptionString}", ex.ToString());
                    if (throwOnException)
                    {
                        throw;
                    }
                    else
                    {
                        return new BatchMessageProcessorResult<object>(messages);
                    }
                }
                finally
                {
                    var metric = _telemetryClient.GetMetric($"BatchMessageProcessor{(success ? "Success" : "Failure")}DurationMs", "SchemaName");
                    metric.TrackValue(stopwatch.Elapsed.TotalMilliseconds, schemaName);
                }

                return MakeGenericResult(task);
            }
            else
            {
                var processorType = typeof(IMessageProcessor<>).MakeGenericType(messageType);
                var processor = _serviceProvider.GetService(processorType);

                if (processor == null)
                {
                    throw new NotSupportedException(
                        $"The message type '{messageType.FullName}', " +
                        $"schema name '{schemaName}', " +
                        $"schema version {schemaVersion} is not supported.");
                }

                var processAsyncMethod = processorType.GetMethod(nameof(IMessageProcessor<object>.ProcessAsync), new Type[] { messageType, typeof(int) });

                // Execute the single message processor, for each message.
                var failed = new List<object>();
                foreach (var message in messages)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var success = false;
                    try
                    {
                        await (Task)processAsyncMethod.Invoke(processor, new object[] { message, dequeueCount });
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("An exception occurred." + Environment.NewLine + "{ExceptionString}", ex.ToString());
                        if (throwOnException)
                        {
                            throw;
                        }
                        else
                        {
                            failed.Add(message);
                        }
                    }
                    finally
                    {
                        var metric = _telemetryClient.GetMetric($"MessageProcessor{(success ? "Success" : "Failure")}DurationMs", "SchemaName");
                        metric.TrackValue(stopwatch.Elapsed.TotalMilliseconds, schemaName);
                    }
                }

                if (failed.Any())
                {
                    return new BatchMessageProcessorResult<object>(failed, Array.Empty<object>(), TimeSpan.Zero);
                }
                else
                {
                    return BatchMessageProcessorResult<object>.Empty;
                }
            }
        }

        private static BatchMessageProcessorResult<object> MakeGenericResult(Task task)
        {
            // We need to turn this:
            //   BatchMessageProcessorResult<T>
            // into this:
            //   BatchMessageProcessorResult<object>
            //
            // This is done by converting the single TryAgainLater property of type:
            //   IReadOnlyDictionary<TimeSpan, IReadOnlyList<T>>
            // into this:
            //   IReadOnlyDictionary<TimeSpan, IReadOnlyList<object>>

            var result = task.GetType().GetProperty(nameof(Task<object>.Result)).GetMethod.Invoke(task, parameters: null);
            var resultType = result.GetType();
            var stronglyTypedFailed = (IEnumerable)resultType.GetProperty(nameof(BatchMessageProcessorResult<object>.Failed)).GetMethod.Invoke(result, parameters: null);
            var stronglyTypedTryAgainLater = (IEnumerable)resultType.GetProperty(nameof(BatchMessageProcessorResult<object>.TryAgainLater)).GetMethod.Invoke(result, parameters: null);

            var failedEnumerator = stronglyTypedFailed.GetEnumerator();
            var tryAgainLaterEnumerator = stronglyTypedTryAgainLater.GetEnumerator();
            if (!failedEnumerator.MoveNext() && !tryAgainLaterEnumerator.MoveNext())
            {
                return BatchMessageProcessorResult<object>.Empty;
            }

            var pairType = tryAgainLaterEnumerator.Current.GetType();
            var getKey = pairType.GetProperty(nameof(KeyValuePair<object, object>.Key)).GetMethod;
            var getValue = pairType.GetProperty(nameof(KeyValuePair<object, object>.Value)).GetMethod;

            var tryAgainLater = new Dictionary<TimeSpan, IReadOnlyList<object>>();
            foreach (var pair in stronglyTypedTryAgainLater)
            {
                var key = getKey.Invoke(pair, parameters: null);
                var value = getValue.Invoke(pair, parameters: null);
                tryAgainLater.Add((TimeSpan)key, ((IEnumerable)value).Cast<object>().ToList());
            }

            return new BatchMessageProcessorResult<object>(
                stronglyTypedFailed.Cast<object>().ToList(),
                tryAgainLater);
        }

        private static object MakeListOfT(Type type, IEnumerable items)
        {
            var listType = typeof(List<>).MakeGenericType(type);
            var list = Activator.CreateInstance(listType);
            var addMethod = listType.GetMethod(nameof(List<object>.Add), new Type[] { type });
            foreach (var item in items)
            {
                addMethod.Invoke(list, new object[] { item });
            }

            return list;
        }
    }
}

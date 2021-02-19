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
    public class GenericMessageProcessor : IGenericMessageProcessor
    {
        private readonly SchemaSerializer _serializer;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IRawMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<GenericMessageProcessor> _logger;

        public GenericMessageProcessor(
            SchemaSerializer serializer,
            IServiceProvider serviceProvider,
            IRawMessageEnqueuer messageEnqueuer,
            ITelemetryClient telemetryClient,
            ILogger<GenericMessageProcessor> logger)
        {
            _serializer = serializer;
            _serviceProvider = serviceProvider;
            _telemetryClient = telemetryClient;
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task ProcessSingleAsync(string message, int dequeueCount)
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

            await ProcessSingleMessageAsync(
                deserializedMessage.SchemaName,
                deserializedMessage.SchemaVersion,
                deserializedMessage.Data,
                dequeueCount);
        }

        private async Task ProcessSingleMessageAsync(string schemaName, int schameVersion, object data, int dequeueCount)
        {
            var result = await ProcessAsync(
                schemaName,
                schameVersion,
                data.GetType(),
                new[] { data },
                dequeueCount,
                throwOnException: true);

            if (result.TryAgainLater.Count == 0
                && result.Failed.Count == 1
                && ReferenceEquals(data, result.Failed[0]))
            {
                throw new InvalidOperationException("A batch containing a single message failed.");
            }

            var serializer = _serializer.GetGenericSerializer(data.GetType());

            await ProcessResultAsync(messageCount: 1, serializer, result);
        }

        public async Task ProcessBatchAsync(string schemaName, int schemaVersion, IReadOnlyList<JToken> data, int dequeueCount)
        {
            if (data.Count == 0)
            {
                return;
            }

            var deserializer = _serializer.GetDeserializer(schemaName);

            // Process a single message without specially, without catching exceptions.
            if (data.Count == 1)
            {
                await ProcessSingleMessageAsync(
                    schemaName,
                    schemaVersion,
                    deserializer.Deserialize(schemaVersion, data[0]),
                    dequeueCount);
                return;
            }

            var serializer = _serializer.GetGenericSerializer(deserializer.Type);

            // This batch has failed before, so split it up instead of processing it immediately.
            if (dequeueCount > 1 && data.Count > 1)
            {
                await _messageEnqueuer.AddAsync(data
                    .Select(x => NameVersionSerializer.SerializeMessage(schemaName, schemaVersion, x).AsString())
                    .ToList());
                return;
            }

            var result = await ProcessAsync(
                schemaName,
                schemaVersion,
                deserializer.Type,
                data.Select(x => deserializer.Deserialize(schemaVersion, x)).ToList(),
                dequeueCount,
                throwOnException: false);

            await ProcessResultAsync(data.Count, serializer, result);
        }

        private async Task ProcessResultAsync(int messageCount, ISchemaSerializer serializer, BatchMessageProcessorResult<object> result)
        {
            if (result.Failed.Any())
            {
                _logger.LogError("{ErrorCount} messages in a batch of {Count} failed. Retrying messages individually.", result.Failed.Count, messageCount);
                await _messageEnqueuer.AddAsync(result.Failed.Select(x => serializer.SerializeMessage(x).AsString()).ToList());
            }

            if (result.TryAgainLater.Any())
            {
                foreach ((var notBefore, var tryAgainLater) in result.TryAgainLater.OrderBy(x => x.Key))
                {
                    if (tryAgainLater.Any())
                    {
                        _logger.LogInformation("{TryAgainLaterCount} messages in a batch of {Count} need to be tried again in {NotBefore}. Retrying messages individually.", tryAgainLater.Count, notBefore, messageCount);
                        await _messageEnqueuer.AddAsync(tryAgainLater.Select(x => serializer.SerializeMessage(x).AsString()).ToList(), notBefore);
                    }
                }
            }
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
                var status = "Exception";
                try
                {
                    var task = (Task)processAsyncMethod.Invoke(batchProcessor, new object[] { stronglyTypedMessages, dequeueCount });
                    await task;
                    var result = MakeGenericResult(messageType, task);

                    status = result.Failed.Count > 0 ? "Failure" : "Success";

                    return result;
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
                    var metric = _telemetryClient.GetMetric($"BatchMessageProcessor{status}DurationMs", "SchemaName");
                    metric.TrackValue(stopwatch.Elapsed.TotalMilliseconds, schemaName);
                }
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
                        var metric = _telemetryClient.GetMetric($"MessageProcessor{(success ? "Success" : "Exception")}DurationMs", "SchemaName");
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

        private static BatchMessageProcessorResult<object> MakeGenericResult(Type messageType, Task task)
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

            var pairType = typeof(KeyValuePair<,>).MakeGenericType(typeof(TimeSpan), typeof(IReadOnlyList<>).MakeGenericType(messageType));
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

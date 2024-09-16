// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

#nullable enable

namespace NuGet.Insights
{
    public class RetryHttpMessageHandler : PolicyHttpMessageHandler
    {
        public const string OnRetryAsyncKey = "NuGet.Insights.OnRetryAsync";
        public const string DelaySourceKey = "NuGet.Insights.DelaySource";

        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<RetryHttpMessageHandler> _logger;
        private readonly IMetric _statusCodeMetric;
        private readonly IMetric _exceptionMetric;
        private readonly IMetric _attemptMetric;

        public RetryHttpMessageHandler(
            IOptions<NuGetInsightsSettings> options,
            ITelemetryClient telemetryClient,
            ILogger<RetryHttpMessageHandler> logger) : base(GetPolicy(options))
        {
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;

            _statusCodeMetric = _telemetryClient.GetMetric($"{nameof(RetryHttpMessageHandler)}.RetryStatusCode.SleepDurationSeconds", "StatusCode", "DelaySource");
            _exceptionMetric = _telemetryClient.GetMetric($"{nameof(RetryHttpMessageHandler)}.RetryException.SleepDurationSeconds", "ExceptionType", "DelaySource");
            _attemptMetric = _telemetryClient.GetMetric($"{nameof(RetryHttpMessageHandler)}.RetryAttempt");
        }

        protected override Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, Context context, CancellationToken cancellationToken)
        {
            context.TryAdd(OnRetryAsyncKey, (object)OnRetryAsync);
            return base.SendCoreAsync(request, context, cancellationToken);
        }

        private Task OnRetryAsync(DelegateResult<HttpResponseMessage> result, TimeSpan sleepDuration, int retryAttempt, Context context, string delaySource)
        {
            _attemptMetric.TrackValue(retryAttempt);

            if (result.Result is not null)
            {
                _statusCodeMetric.TrackValue(
                    sleepDuration.TotalSeconds,
                    ((int)result.Result.StatusCode).ToString(CultureInfo.InvariantCulture),
                    delaySource);
            }
            else
            {
                _exceptionMetric.TrackValue(
                    sleepDuration.TotalSeconds,
                    result.Exception.GetType().FullName,
                    delaySource);
            }

            return Task.CompletedTask;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetPolicy(IOptions<NuGetInsightsSettings> options)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(x => x.StatusCode == HttpStatusCode.TooManyRequests)
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(
                    retryCount: options.Value.HttpClientMaxRetries,
                    sleepDurationProvider: (retryAttempt, result, context) =>
                    {
                        TimeSpan delay = default;
                        string? delaySource = null;

                        var retryAfter = result.Result?.Headers.RetryAfter;
                        if (retryAfter is not null)
                        {
                            if (retryAfter.Delta.HasValue)
                            {
                                delay = retryAfter.Delta.Value;
                                delaySource = "Retry-After delta";
                            }
                            else if (retryAfter.Date.HasValue)
                            {
                                delay = retryAfter.Date.Value - DateTimeOffset.UtcNow;
                                delaySource = $"Retry-After date";
                            }
                        }

                        if (delaySource is null || delay <= TimeSpan.Zero)
                        {
                            delay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                            delaySource = "exponential back-off";
                        }

                        if (options.Value.HttpClientAddRetryJitter)
                        {
                            delay += TimeSpan.FromMilliseconds(Random.Shared.Next(0, retryAttempt * 1000));
                        }

                        if (delay > options.Value.HttpClientMaxRetryDelay)
                        {
                            delay = options.Value.HttpClientMaxRetryDelay;
                            delaySource = $"max retry delay, was {delaySource}";
                        }

                        context[DelaySourceKey] = delaySource;

                        return delay;
                    },
                    onRetryAsync: (result, timeSpan, retryAttempt, context) =>
                    {
                        if (context.TryGetValue(DelaySourceKey, out var delaySourceObj)
                            && delaySourceObj is string delaySource
                            && context.TryGetValue(OnRetryAsyncKey, out var onRetryAsyncObj)
                            && onRetryAsyncObj is Func<DelegateResult<HttpResponseMessage>, TimeSpan, int, Context, string, Task> onRetryAsync)
                        {
                            return onRetryAsync(result, timeSpan, retryAttempt, context, delaySource);
                        }

                        return Task.CompletedTask;
                    });
        }
    }
}

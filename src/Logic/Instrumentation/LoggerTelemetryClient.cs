// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class LoggerTelemetryClient : ITelemetryClient
    {
        private readonly Func<MetricKey, bool> _shouldIgnore;
        private readonly ILogger<LoggerTelemetryClient> _logger;
        private readonly ConcurrentDictionary<MetricKey, LoggerMetric> _metrics = new();

        public LoggerTelemetryClient(Func<MetricKey, bool> shouldIgnore, ILogger<LoggerTelemetryClient> logger)
        {
            _shouldIgnore = shouldIgnore;
            _logger = logger;
        }

        private ILogger GetLogger(MetricKey k)
        {
            if (k.MetricId.Contains("..", StringComparison.Ordinal))
            {
                throw new ArgumentException("Metric IDs cannot contain two consecutive periods.", nameof(k));
            }

            return _shouldIgnore(k) ? NullLogger.Instance : _logger;
        }

        public IReadOnlyDictionary<MetricKey, LoggerMetric> Metrics => _metrics;

        public void Clear()
        {
            foreach (var metric in _metrics)
            {
                metric.Value.MetricValues.Clear();
            }
        }

        public IMetric GetMetric(string metricId)
        {
            return _metrics.GetOrAdd(
                new MetricKey(metricId),
                k => new LoggerMetric(metricId, [], GetLogger(k)));
        }

        public IMetric GetMetric(string metricId, string dimension1Name)
        {
            return _metrics.GetOrAdd(
                new MetricKey(metricId, dimension1Name),
                k => new LoggerMetric(metricId, [dimension1Name], GetLogger(k)));
        }

        public IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name)
        {
            return _metrics.GetOrAdd(
                new MetricKey(metricId, dimension1Name, dimension2Name),
                k => new LoggerMetric(metricId, [dimension1Name, dimension2Name], GetLogger(k)));
        }

        public IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name, string dimension3Name)
        {
            return _metrics.GetOrAdd(
                new MetricKey(metricId, dimension1Name, dimension2Name, dimension3Name),
                k => new LoggerMetric(metricId, [dimension1Name, dimension2Name, dimension3Name], GetLogger(k)));
        }

        public IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name, string dimension3Name, string dimension4Name)
        {
            return _metrics.GetOrAdd(
                new MetricKey(metricId, dimension1Name, dimension2Name, dimension3Name, dimension4Name),
                k => new LoggerMetric(metricId, [dimension1Name, dimension2Name, dimension3Name, dimension4Name], GetLogger(k)));
        }

        public ConcurrentQueue<string> Operations { get; } = new();

        public IDisposable StartOperation(string operationName)
        {
            Operations.Enqueue(operationName);
            return GetLogger(new MetricKey(operationName)).BeginScope("Telemetry operation: {Scope_OperationName}", operationName);
        }

        public ConcurrentQueue<(string MetricId, double MetricValue, IDictionary<string, string> MetricProperties)> MetricValues { get; } = new();

        public void TrackMetric(string name, double value, IDictionary<string, string> properties)
        {
            MetricValues.Enqueue((name, value, properties));

            var logger = GetLogger(new MetricKey(name));
            if (properties.Count == 0)
            {
                logger.LogInformation("Metric emitted: {MetricName} = {MetricValue}", name, value);
            }
            else
            {
                logger.LogInformation("Metric emitted: {MetricName} = {MetricValue} with properties {Properties}", name, value, JsonSerializer.Serialize(properties));
            }
        }

        public record Operation(string OperationName);

        public record MetricKey(
            string MetricId,
            string Dimension1Name = null,
            string Dimension2Name = null,
            string Dimension3Name = null,
            string Dimension4Name = null);
    }
}

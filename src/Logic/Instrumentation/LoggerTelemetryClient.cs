// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class LoggerTelemetryClient : ITelemetryClient
    {
        private readonly Func<MetricKey, bool> _shouldIgnore;
        private readonly ILogger<LoggerTelemetryClient> _logger;

        public LoggerTelemetryClient(ILogger<LoggerTelemetryClient> logger)
            : this(_ => false, logger)
        {
        }

        public LoggerTelemetryClient(Func<MetricKey, bool> shouldIgnore, ILogger<LoggerTelemetryClient> logger)
        {
            _shouldIgnore = shouldIgnore;
            _logger = logger;
        }

        private ILogger GetLogger(MetricKey k)
        {
            return _shouldIgnore(k) ? NullLogger.Instance : _logger;
        }

        public ConcurrentDictionary<MetricKey, LoggerMetric> Metrics { get; } = new();

        public IMetric GetMetric(string metricId)
        {
            return Metrics.GetOrAdd(
                new MetricKey(metricId),
                k => new LoggerMetric(metricId, Array.Empty<string>(), GetLogger(k)));
        }

        public IMetric GetMetric(string metricId, string dimension1Name)
        {
            return Metrics.GetOrAdd(
                new MetricKey(metricId, dimension1Name),
                k => new LoggerMetric(metricId, new[] { dimension1Name }, GetLogger(k)));
        }

        public IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name)
        {
            return Metrics.GetOrAdd(
                new MetricKey(metricId, dimension1Name, dimension2Name),
                k => new LoggerMetric(metricId, new[] { dimension1Name, dimension2Name }, GetLogger(k)));
        }

        public IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name, string dimension3Name)
        {
            return Metrics.GetOrAdd(
                new MetricKey(metricId, dimension1Name, dimension2Name, dimension3Name),
                k => new LoggerMetric(metricId, new[] { dimension1Name, dimension2Name, dimension3Name }, GetLogger(k)));
        }

        public IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name, string dimension3Name, string dimension4Name)
        {
            return Metrics.GetOrAdd(
                new MetricKey(metricId, dimension1Name, dimension2Name, dimension3Name, dimension4Name),
                k => new LoggerMetric(metricId, new[] { dimension1Name, dimension2Name, dimension3Name, dimension4Name }, GetLogger(k)));
        }

        public ConcurrentQueue<string> Operations { get; } = new();

        public IDisposable StartOperation(string operationName)
        {
            Operations.Enqueue(operationName);
            return _logger.BeginScope(new { operationName });
        }

        public ConcurrentQueue<(string MetricId, double MetricValue, IDictionary<string, string> MetricProperties)> MetricValues { get; } = new();

        public void TrackMetric(string name, double value, IDictionary<string, string> properties)
        {
            MetricValues.Enqueue((name, value, properties));

            if (properties.Count == 0)
            {
                _logger.LogInformation("Metric emitted: {MetricName} = {MetricValue}", name, value);
            }
            else
            {
                _logger.LogInformation("Metric emitted: {MetricName} = {MetricValue} with properties {Properties}", name, value, JsonSerializer.Serialize(properties));
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

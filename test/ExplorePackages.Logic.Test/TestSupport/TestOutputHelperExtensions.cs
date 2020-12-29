using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages
{
    public static class TestOutputHelperExtensions
    {
        public static ILogger<T> GetLogger<T>(this ITestOutputHelper output)
        {
            var factory = new LoggerFactory();
            factory.AddXunit(output);
            return factory.CreateLogger<T>();
        }

        public static ITelemetryClient GetTelemetryClient(this ITestOutputHelper output)
        {
            return new LoggerTelemetryClient(output.GetLogger<LoggerTelemetryClient>());
        }

        private class LoggerTelemetryClient : ITelemetryClient
        {
            private readonly ILogger<LoggerTelemetryClient> _logger;

            public LoggerTelemetryClient(ILogger<LoggerTelemetryClient> logger)
            {
                _logger = logger;
            }

            public IMetric GetMetric(string metricId) => new LoggerMetric(metricId, Array.Empty<string>(), _logger);
            public IMetric GetMetric(string metricId, string dimension1Name) => new LoggerMetric(metricId, new[] { dimension1Name }, _logger);
            public IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name) => new LoggerMetric(metricId, new[] { dimension1Name, dimension2Name }, _logger);

            public void TrackMetric(string name, double value, IDictionary<string, string> properties)
            {
                _logger.LogInformation("Metric emitted: {MetricName} = {MetricValue} with properties {Properties}", name, value, JsonConvert.SerializeObject(properties));
            }
        }

        private class LoggerMetric : IMetric
        {
            private readonly string _metricId;
            private readonly IReadOnlyList<string> _dimensionNames;
            private readonly ILogger _logger;

            public LoggerMetric(string metricId, IReadOnlyList<string> dimensionNames, ILogger logger)
            {
                _metricId = metricId;
                _dimensionNames = dimensionNames;
                _logger = logger;
            }

            public void TrackValue(double metricValue)
            {
                AssertDimensionCount(0);
                _logger.LogInformation("Metric emitted: {MetricId} = {MetricValue}", _metricId, metricValue);
            }

            private void AssertDimensionCount(int valueCount)
            {
                if (_dimensionNames.Count != valueCount)
                {
                    throw new InvalidOperationException($"There are {_dimensionNames.Count} dimension names but only {valueCount} dimension values were provided.");
                }
            }

            public bool TrackValue(double metricValue, string dimension1Value)
            {
                AssertDimensionCount(1);
                _logger.LogInformation("Metric emitted: {MetricId} {Dimension1Name} = {MetricValue}", _metricId, dimension1Value, metricValue);
                return true;
            }

            public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value)
            {
                AssertDimensionCount(2);
                _logger.LogInformation("Metric emitted: {MetricId} {Dimension1Name} {Dimension2Name} = {MetricValue}", _metricId, dimension1Value, dimension2Value, metricValue);
                return true;
            }
        }
    }
}

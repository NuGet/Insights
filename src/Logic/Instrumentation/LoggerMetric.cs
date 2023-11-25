// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class LoggerMetric : IMetric
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

        public ConcurrentQueue<(double MetricValue, string[] DimensionValues)> MetricValues { get; } = new();
        public static object GenericMessageProcessor { get; private set; }

        public void TrackValue(double metricValue)
        {
            AssertDimensionCount(0);
            MetricValues.Enqueue((metricValue, Array.Empty<string>()));
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
            MetricValues.Enqueue((metricValue, [dimension1Value]));
            _logger.LogInformation("Metric emitted: {MetricId} {Dimension1Name} = {MetricValue}", _metricId, dimension1Value, metricValue);
            return true;
        }

        public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value)
        {
            AssertDimensionCount(2);
            MetricValues.Enqueue((metricValue, [dimension1Value, dimension2Value]));
            _logger.LogInformation("Metric emitted: {MetricId} {Dimension1Name} {Dimension2Name} = {MetricValue}", _metricId, dimension1Value, dimension2Value, metricValue);
            return true;
        }

        public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value, string dimension3Value)
        {
            AssertDimensionCount(3);
            MetricValues.Enqueue((metricValue, [dimension1Value, dimension2Value, dimension3Value]));
            _logger.LogInformation("Metric emitted: {MetricId} {Dimension1Name} {Dimension2Name} {Dimension3Name} = {MetricValue}", _metricId, dimension1Value, dimension2Value, dimension3Value, metricValue);
            return true;
        }

        public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value, string dimension3Value, string dimension4Value)
        {
            AssertDimensionCount(4);
            MetricValues.Enqueue((metricValue, [dimension1Value, dimension2Value, dimension3Value, dimension4Value]));
            _logger.LogInformation("Metric emitted: {MetricId} {Dimension1Name} {Dimension2Name} {Dimension3Name} {Dimension4Name} = {MetricValue}", _metricId, dimension1Value, dimension2Value, dimension3Value, dimension4Value, metricValue);
            return true;
        }
    }
}

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

        public LimitedConcurrentQueue<(double MetricValue, string[] DimensionValues)> MetricValues { get; } = new(limit: 1000);
        public static object GenericMessageProcessor { get; private set; }

        public void TrackValue(double metricValue)
        {
            AssertDimensionCount(0);
            Enqueue((metricValue, []));
            _logger.LogInformation("Metric emitted: {MetricId} = {MetricValue}", _metricId, metricValue);
        }

        private void Enqueue((double MetricValue, string[] DimensionValues) input)
        {
            MetricValues.Enqueue(input, limit => _logger.LogTransientWarning(
                "The metric value queue for {MetricId} has exceeded its limit of {Limit}. Older values will be dropped.",
                _metricId,
                limit));
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
            string[] dimensions = [dimension1Value];
            AssertNoNullOrEmptyDimensions(dimensions);
            Enqueue((metricValue, dimensions));
            _logger.LogInformation(
                "Metric emitted: {MetricId} ({Dimension1Name}, {Dimension1Value}) = {MetricValue}",
                _metricId,
                _dimensionNames[0],
                dimension1Value,
                metricValue);
            return true;
        }

        public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value)
        {
            AssertDimensionCount(2);
            string[] dimensions = [dimension1Value, dimension2Value];
            AssertNoNullOrEmptyDimensions(dimensions);
            Enqueue((metricValue, dimensions));
            _logger.LogInformation(
                "Metric emitted: {MetricId} ({Dimension1Name}, {Dimension1Value}) ({Dimension2Name}, {Dimension2Value}) = {MetricValue}",
                _metricId,
                _dimensionNames[0],
                dimension1Value,
                _dimensionNames[1],
                dimension2Value,
                metricValue);
            return true;
        }

        public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value, string dimension3Value)
        {
            AssertDimensionCount(3);
            string[] dimensions = [dimension1Value, dimension2Value, dimension3Value];
            AssertNoNullOrEmptyDimensions(dimensions);
            Enqueue((metricValue, dimensions));
            _logger.LogInformation(
                "Metric emitted: {MetricId} ({Dimension1Name}, {Dimension1Value}) ({Dimension2Name}, {Dimension2Value}) ({Dimension3Name}, {Dimension3Value}) = {MetricValue}",
                _metricId,
                _dimensionNames[0],
                dimension1Value,
                _dimensionNames[1],
                dimension2Value,
                _dimensionNames[2],
                dimension3Value,
                metricValue);
            return true;
        }

        public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value, string dimension3Value, string dimension4Value)
        {
            AssertDimensionCount(4);
            string[] dimensions = [dimension1Value, dimension2Value, dimension3Value, dimension4Value];
            AssertNoNullOrEmptyDimensions(dimensions);
            Enqueue((metricValue, dimensions));
            _logger.LogInformation(
                "Metric emitted: {MetricId} ({Dimension1Name}, {Dimension1Value}) ({Dimension2Name}, {Dimension2Value}) ({Dimension3Name}, {Dimension3Value}) ({Dimension4Name}, {Dimension4Value}) = {MetricValue}",
                _metricId,
                _dimensionNames[0],
                dimension1Value,
                _dimensionNames[1],
                dimension2Value,
                _dimensionNames[2],
                dimension3Value,
                _dimensionNames[3],
                dimension4Value,
                metricValue);
            return true;
        }

        private void AssertNoNullOrEmptyDimensions(string[] dimensions)
        {
            for (var i = 0; i < dimensions.Length; i++)
            {
                if (string.IsNullOrEmpty(dimensions[i]))
                {
                    throw new InvalidOperationException($"Dimension {i} is null or empty.");
                }
            }
        }
    }
}

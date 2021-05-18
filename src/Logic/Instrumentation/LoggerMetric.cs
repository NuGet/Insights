// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
            _logger = ShouldLogMetric(metricId) ? logger : NullLogger.Instance;
        }

        private static bool ShouldLogMetric(string metricId)
        {
            if (metricId.Contains(QueryLoopMetrics.MetricIdSubstring))
            {
                return false;
            }

            return true;
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

        public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value, string dimension3Value)
        {
            AssertDimensionCount(3);
            _logger.LogInformation("Metric emitted: {MetricId} {Dimension1Name} {Dimension2Name} {Dimension3Name} = {MetricValue}", _metricId, dimension1Value, dimension2Value, dimension3Value, metricValue);
            return true;
        }
    }
}

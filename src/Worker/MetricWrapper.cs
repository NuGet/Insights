// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights;

namespace NuGet.Insights.Worker
{
    public class MetricWrapper : IMetric
    {
        private readonly Metric _inner;

        public MetricWrapper(Metric metric)
        {
            _inner = metric;
        }

        public void TrackValue(double metricValue)
        {
            _inner.TrackValue(metricValue);
        }

        public bool TrackValue(double metricValue, string dimension1Value)
        {
            return _inner.TrackValue(metricValue, dimension1Value);
        }

        public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value)
        {
            return _inner.TrackValue(metricValue, dimension1Value, dimension2Value);
        }

        public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value, string dimension3Value)
        {
            return _inner.TrackValue(metricValue, dimension1Value, dimension2Value, dimension3Value);
        }

        public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value, string dimension3Value, string dimension4Value)
        {
            return _inner.TrackValue(metricValue, dimension1Value, dimension2Value, dimension3Value, dimension4Value);
        }
    }
}

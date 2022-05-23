// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.ApplicationInsights;

namespace NuGet.Insights
{
    public class TelemetryClientWrapper : ITelemetryClient
    {
        private readonly TelemetryClient _inner;

        public TelemetryClientWrapper(TelemetryClient inner)
        {
            _inner = inner;
        }

        public IMetric GetMetric(string metricId)
        {
            return new MetricWrapper(_inner.GetMetric(metricId));
        }

        public IMetric GetMetric(string metricId, string dimension1Name)
        {
            return new MetricWrapper(_inner.GetMetric(metricId, dimension1Name));
        }

        public IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name)
        {
            return new MetricWrapper(_inner.GetMetric(metricId, dimension1Name, dimension2Name));
        }

        public IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name, string dimension3Name)
        {
            return new MetricWrapper(_inner.GetMetric(metricId, dimension1Name, dimension2Name, dimension3Name));
        }

        public IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name, string dimension3Name, string dimension4Name)
        {
            return new MetricWrapper(_inner.GetMetric(metricId, dimension1Name, dimension2Name, dimension3Name, dimension4Name));
        }

        public void TrackMetric(string name, double value, IDictionary<string, string> properties)
        {
            _inner.TrackMetric(name, value, properties);
        }
    }
}

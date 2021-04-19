using System.Collections.Generic;
using Microsoft.ApplicationInsights;

namespace Knapcode.ExplorePackages.Worker
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

        public void TrackMetric(string name, double value, IDictionary<string, string> properties)
        {
            _inner.TrackMetric(name, value, properties);
        }
    }
}

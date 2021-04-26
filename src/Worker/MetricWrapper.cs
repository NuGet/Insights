using Microsoft.ApplicationInsights;

namespace Knapcode.ExplorePackages.Worker
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
    }
}

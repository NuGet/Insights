using System.Collections.Generic;

namespace Knapcode.ExplorePackages
{
    public class NullTelemetryClient : ITelemetryClient
    {
        public static NullTelemetryClient Instance { get; } = new NullTelemetryClient();

        public IMetric GetMetric(string metricId) => NullMetric.Instance;
        public IMetric GetMetric(string metricId, string dimension1Name) => NullMetric.Instance;
        public IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name) => NullMetric.Instance;
        public void TrackMetric(string name, double value, IDictionary<string, string> properties)
        {
        }
    }
}

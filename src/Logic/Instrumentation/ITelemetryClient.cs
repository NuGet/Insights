using System.Collections.Generic;

namespace NuGet.Insights
{
    public interface ITelemetryClient
    {
        IMetric GetMetric(string metricId);
        IMetric GetMetric(string metricId, string dimension1Name);
        IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name);
        IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name, string dimension3Name);
        void TrackMetric(string name, double value, IDictionary<string, string> properties);
    }
}

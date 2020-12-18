namespace Knapcode.ExplorePackages
{
    public interface ITelemetryClient
    {
        IMetric GetMetric(string metricId, string dimension1Name);
        IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name);
    }
}

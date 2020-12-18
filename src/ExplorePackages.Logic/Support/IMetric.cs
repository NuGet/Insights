namespace Knapcode.ExplorePackages
{
    public interface IMetric
    {
        bool TrackValue(double metricValue, string dimension1Value);
        bool TrackValue(double metricValue, string dimension1Value, string dimension2Value);
    }
}

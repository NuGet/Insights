namespace Knapcode.ExplorePackages
{
    public class NullMetric : IMetric
    {
        public static NullMetric Instance { get; } = new NullMetric();

        public void TrackValue(double metricValue)
        {
        }

        public bool TrackValue(double metricValue, string dimension1Value) => true;
        public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value) => true;
    }
}

namespace NuGet.Insights.Worker
{
    public class BulkEnqueueStrategy
    {
        private BulkEnqueueStrategy(bool isEnabled, int threshold)
        {
            IsEnabled = isEnabled;
            Threshold = threshold;
        }

        public static BulkEnqueueStrategy Enabled(int threshold)
        {
            return new BulkEnqueueStrategy(isEnabled: true, threshold);
        }

        public static BulkEnqueueStrategy Disabled()
        {
            return new BulkEnqueueStrategy(isEnabled: false, threshold: 0);
        }

        public bool IsEnabled { get; }
        public int Threshold { get; }
    }
}
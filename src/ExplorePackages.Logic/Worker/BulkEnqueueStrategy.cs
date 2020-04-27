namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class BulkEnqueueStrategy
    {
        private BulkEnqueueStrategy(bool isEnabled, int threshold, int maxSize)
        {
            IsEnabled = isEnabled;
            Threshold = threshold;
            MaxSize = maxSize;
        }

        public static BulkEnqueueStrategy Enabled(int threshold, int maxSize)
        {
            return new BulkEnqueueStrategy(isEnabled: true, threshold: threshold, maxSize: maxSize);
        }

        public static BulkEnqueueStrategy Disabled()
        {
            return new BulkEnqueueStrategy(isEnabled: false, threshold: 0, maxSize: 0);
        }

        public bool IsEnabled { get; }
        public int Threshold { get; }
        public int MaxSize { get; }
    }
}
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class PartialProgressReporter : IProgressReporter
    {
        private readonly IProgressReporter _innerProgressReporter;
        private readonly decimal _min;
        private readonly decimal _difference;

        public PartialProgressReporter(IProgressReporter innerProgressReporter, decimal min, decimal max)
        {
            _innerProgressReporter = innerProgressReporter;
            _min = min;
            _difference = max - min;
        }

        public Task ReportProgressAsync(decimal percent, string message)
        {
            var partialPercent = _min + (percent * _difference);
            return _innerProgressReporter.ReportProgressAsync(partialPercent, message);
        }
    }
}

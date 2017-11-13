using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class PartialProgressReport : IProgressReport
    {
        private readonly IProgressReport _innerProgressReport;
        private readonly decimal _min;
        private readonly decimal _difference;

        public PartialProgressReport(IProgressReport innerProgressReport, decimal min, decimal max)
        {
            _innerProgressReport = innerProgressReport;
            _min = min;
            _difference = max - min;
        }

        public Task ReportProgressAsync(decimal percent, string message)
        {
            var partialPercent = _min + (percent * _difference);
            return _innerProgressReport.ReportProgressAsync(partialPercent, message);
        }
    }
}

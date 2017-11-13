using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class IncrementalProgress
    {
        private readonly IProgressReport _progressReport;
        private int _current;
        private readonly decimal _total;

        public IncrementalProgress(IProgressReport progressReport, int total)
        {
            _progressReport = progressReport;
            _current = 0;
            _total = total;
        }

        public Task ReportProgressAsync(string message)
        {
            if (_current < _total)
            {
                _current++;
            }

            return _progressReport.ReportProgressAsync(_current / _total, message);
        }
    }
}

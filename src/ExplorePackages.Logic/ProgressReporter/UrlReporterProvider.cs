using System.Threading;

namespace Knapcode.ExplorePackages.Logic
{
    public class UrlReporterProvider
    {
        private readonly AsyncLocal<IUrlReporter> _currentUrlReport;

        public UrlReporterProvider()
        {
             _currentUrlReport = new AsyncLocal<IUrlReporter>();
        }

        public void SetUrlReport(IUrlReporter urlReport)
        {
            _currentUrlReport.Value = urlReport;
        }

        public IUrlReporter GetUrlReport()
        {
            return _currentUrlReport.Value ?? NullUrlReporter.Instance;
        }
    }
}

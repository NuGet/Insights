using System.Threading;

namespace Knapcode.ExplorePackages.Logic
{
    public class UrlReportProvider
    {
        private readonly AsyncLocal<IUrlReport> _currentUrlReport;

        public UrlReportProvider()
        {
             _currentUrlReport = new AsyncLocal<IUrlReport>();
        }

        public void SetUrlReport(IUrlReport urlReport)
        {
            _currentUrlReport.Value = urlReport;
        }

        public IUrlReport GetUrlReport()
        {
            return _currentUrlReport.Value ?? NullUrlReport.Instance;
        }
    }
}

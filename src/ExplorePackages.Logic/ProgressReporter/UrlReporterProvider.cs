using System.Threading;

namespace Knapcode.ExplorePackages.Logic
{
    public class UrlReporterProvider
    {
        private readonly AsyncLocal<IUrlReporter> _currentUrlReporter;

        public UrlReporterProvider()
        {
             _currentUrlReporter = new AsyncLocal<IUrlReporter>();
        }

        public void SetUrlReporter(IUrlReporter urlReporter)
        {
            _currentUrlReporter.Value = urlReporter;
        }

        public IUrlReporter GetUrlReporter()
        {
            return _currentUrlReporter.Value ?? NullUrlReporter.Instance;
        }
    }
}

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class UrlReportHandler : DelegatingHandler
    {
        private readonly UrlReportProvider _provider;

        public UrlReportHandler(UrlReportProvider provider)
        {
            _provider = provider;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await _provider.GetUrlReport().ReportUrlAsync(request.RequestUri);

            return await base.SendAsync(request, cancellationToken);
        }
    }
}

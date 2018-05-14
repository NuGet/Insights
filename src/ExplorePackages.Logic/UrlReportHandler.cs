using System;
using System.Diagnostics;
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
            var urlReport = _provider.GetUrlReport();
            var id = Guid.NewGuid();
            await urlReport.ReportRequestAsync(id, request);
            var stopwatch = Stopwatch.StartNew();
            var response = await base.SendAsync(request, cancellationToken);
            await urlReport.ReportResponseAsync(id, response, stopwatch.Elapsed);
            return response;
        }
    }
}

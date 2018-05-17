using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class UrlReporterHandler : DelegatingHandler
    {
        private readonly UrlReporterProvider _provider;

        public UrlReporterHandler(UrlReporterProvider provider)
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

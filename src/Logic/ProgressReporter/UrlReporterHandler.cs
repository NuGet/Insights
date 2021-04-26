using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
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
            var urlReporter = _provider.GetUrlReporter();
            var id = Guid.NewGuid();
            await urlReporter.ReportRequestAsync(id, request);
            var stopwatch = Stopwatch.StartNew();
            var response = await base.SendAsync(request, cancellationToken);
            await urlReporter.ReportResponseAsync(id, response, stopwatch.Elapsed);
            return response;
        }
    }
}

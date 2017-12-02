using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Support
{
    public class LoggingHandler : DelegatingHandler
    {
        private readonly ILogger _log;

        public LoggingHandler(ILogger log)
        {
            _log = log;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _log.LogInformation($"  {request.Method} {request.RequestUri}");
            var stopwatch = Stopwatch.StartNew();
            var response = await base.SendAsync(request, cancellationToken);
            _log.LogInformation($"  {response.StatusCode} {response.RequestMessage.RequestUri} {stopwatch.ElapsedMilliseconds}ms");
            return response;
        }
    }
}

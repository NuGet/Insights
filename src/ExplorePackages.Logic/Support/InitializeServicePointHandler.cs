using System;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace Knapcode.ExplorePackages.Logic
{
    public class InitializeServicePointHandler : MessageProcessingHandler
    {
        private readonly TimeSpan _connectionLeaseTimeout;

        public InitializeServicePointHandler(TimeSpan connectionLeaseTimeout)
        {
            _connectionLeaseTimeout = connectionLeaseTimeout;
        }

        protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var servicePoint = ServicePointManager.FindServicePoint(request.RequestUri);
            servicePoint.ConnectionLeaseTimeout = (int)_connectionLeaseTimeout.TotalMilliseconds;

            return request;
        }

        protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            return response;
        }
    }
}

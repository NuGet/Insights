using System;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace Knapcode.ExplorePackages.Support
{
    public class InitializeServicePointHandler : MessageProcessingHandler
    {
        private readonly int _connectionLimit;
        private readonly TimeSpan _connectionLeaseTimeout;

        public InitializeServicePointHandler(int connectionLimit, TimeSpan connectionLeaseTimeout)
        {
            _connectionLimit = connectionLimit;
            _connectionLeaseTimeout = connectionLeaseTimeout;
        }

        protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var servicePoint = ServicePointManager.FindServicePoint(request.RequestUri);
            servicePoint.ConnectionLimit = _connectionLimit;
            servicePoint.ConnectionLeaseTimeout = (int)_connectionLeaseTimeout.TotalMilliseconds;

            return request;
        }

        protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            return response;
        }
    }
}

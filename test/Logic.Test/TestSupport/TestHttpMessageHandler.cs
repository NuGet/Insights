// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class TestHttpMessageHandler : DelegatingHandler
    {
        private readonly SendMessageWithBaseAsync _onSendAsync;
        private readonly ConcurrentQueue<HttpRequestMessage> _requestQueue;
        private readonly ConcurrentQueue<HttpResponseMessage> _responseQueue;

        public TestHttpMessageHandler(
            SendMessageWithBaseAsync onSendAsync,
            ConcurrentQueue<HttpRequestMessage> requestQueue,
            ConcurrentQueue<HttpResponseMessage> responseQueue)
        {
            _onSendAsync = onSendAsync;
            _requestQueue = requestQueue;
            _responseQueue = responseQueue;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            _requestQueue.Enqueue(request);

            var response = await _onSendAsync(request, base.SendAsync, token);
            if (response != null)
            {
                _responseQueue.Enqueue(response);
                return response;
            }

            response = await base.SendAsync(request, token);
            _responseQueue.Enqueue(response);
            return response;
        }
    }
}

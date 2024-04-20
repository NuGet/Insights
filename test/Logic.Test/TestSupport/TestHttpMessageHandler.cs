// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class TestHttpMessageHandler : DelegatingHandler
    {
        private readonly SendMessageWithBaseAsync _onSendAsync;
        private readonly ConcurrentQueue<HttpRequestMessage> _requestQueue;
        private readonly ConcurrentQueue<(HttpRequestMessage OriginalRequest, HttpResponseMessage Response)> _responseQueue;

        public TestHttpMessageHandler(
            SendMessageWithBaseAsync onSendAsync,
            ConcurrentQueue<HttpRequestMessage> requestQueue,
            ConcurrentQueue<(HttpRequestMessage OriginalRequest, HttpResponseMessage Response)> responseQueue)
        {
            _onSendAsync = onSendAsync;
            _requestQueue = requestQueue;
            _responseQueue = responseQueue;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var shouldLog = ShouldLog(request);

            if (shouldLog)
            {
                _requestQueue.Enqueue(request);
            }

            var response = await _onSendAsync(request, base.SendAsync, token);

            if (response != null)
            {
                token.ThrowIfCancellationRequested();

                if (shouldLog)
                {
                    _responseQueue.Enqueue((request, response));
                }

                return response;
            }

            response = await base.SendAsync(request, token);

            token.ThrowIfCancellationRequested();

            if (shouldLog)
            {
                _responseQueue.Enqueue((request, response));
            }

            return response;
        }

        private bool ShouldLog(HttpRequestMessage request)
        {
            // exclude user delegation key requests to simplify assertions
            if (request.Method == HttpMethod.Post
                && request.RequestUri is not null
                && request.RequestUri.Query.Contains("restype=service", StringComparison.Ordinal)
                && request.RequestUri.Query.Contains("comp=userdelegationkey", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
    }
}

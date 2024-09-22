// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class TestHttpMessageHandler : DelegatingHandler
    {
        private readonly SendMessageWithBaseAsync _onSendAsync;
        private readonly LimitedConcurrentQueue<HttpRequestMessage> _requestQueue;
        private readonly LimitedConcurrentQueue<(HttpRequestMessage OriginalRequest, HttpResponseMessage Response)> _responseQueue;
        private readonly ILogger<TestHttpMessageHandler> _logger;

        public TestHttpMessageHandler(
            SendMessageWithBaseAsync onSendAsync,
            LimitedConcurrentQueue<HttpRequestMessage> requestQueue,
            LimitedConcurrentQueue<(HttpRequestMessage OriginalRequest, HttpResponseMessage Response)> responseQueue,
            ILogger<TestHttpMessageHandler> logger)
        {
            _onSendAsync = onSendAsync;
            _requestQueue = requestQueue;
            _responseQueue = responseQueue;
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var shouldLog = ShouldLog(request);

            if (shouldLog)
            {
                _requestQueue.Enqueue(request, limit => _logger.LogTransientWarning(
                    "The HTTP request queue has exceeded its limit of {Limit}. Older values will be dropped.",
                    limit));
            }

            var response = await _onSendAsync(request, base.SendAsync, token);

            if (response != null)
            {
                token.ThrowIfCancellationRequested();

                if (shouldLog)
                {
                    _responseQueue.Enqueue((request, response), limit => _logger.LogTransientWarning(
                        "The HTTP response queue has exceeded its limit of {Limit}. Older values will be dropped.",
                        limit));
                }

                return response;
            }

            response = await base.SendAsync(request, token);

            token.ThrowIfCancellationRequested();

            if (shouldLog)
            {
                _responseQueue.Enqueue((request, response), limit => _logger.LogTransientWarning(
                    "The HTTP request queue has exceeded its limit of {Limit}. Older values will be dropped.",
                    limit));
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

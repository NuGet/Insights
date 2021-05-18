// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Insights
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

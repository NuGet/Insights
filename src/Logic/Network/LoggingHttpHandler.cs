// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

#nullable enable

namespace NuGet.Insights
{
    public class LoggingHttpHandler : DelegatingHandler
    {
        private readonly ILogger<LoggingHttpHandler> _logger;

        public LoggingHttpHandler(ILogger<LoggingHttpHandler> logger)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "  {Method} {RequestUri}",
                request.Method,
                request.RequestUri.Obfuscate());
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                _logger.LogInformation(
                    "  {StatusCode} {RequestUri} {ElapsedMs}ms",
                    response.StatusCode,
                    response.RequestMessage?.RequestUri.Obfuscate(),
                    (int)stopwatch.Elapsed.TotalMilliseconds);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogTransientWarning(
                    $"  Error {{RequestUri}} {{ElapsedMs}}ms{Environment.NewLine}{{ExceptionMessage}}",
                    request.RequestUri.Obfuscate(),
                    (int)stopwatch.Elapsed.TotalMilliseconds,
                    ExceptionUtilities.DisplayMessage(ex));
                throw;
            }
        }
    }
}

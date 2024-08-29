// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Microsoft.NET.StringTools;

namespace NuGet.Insights
{
    public class TelemetryHttpHandler : DelegatingHandler
    {
        public const string MetricIdPrefix = "TelemetryHttpHandler.";

        private readonly ITelemetryClient _telemetryClient;
        private readonly IMetric _completedDurationMsMetric;
        private readonly IMetric _exceptionDurationMsMetric;

        public TelemetryHttpHandler(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient;
            _completedDurationMsMetric = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(SendAsync)}.Completed.DurationMs",
                "Method",
                "Host",
                "PathPrefix",
                "StatusCode");
            _exceptionDurationMsMetric = _telemetryClient.GetMetric(
                $"{MetricIdPrefix}{nameof(SendAsync)}.Exception.DurationMs",
                "Method",
                "Host",
                "PathPrefix",
                "ExceptionType");
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var response = await base.SendAsync(request, cancellationToken);

                sw.Stop();
                var method = response.RequestMessage?.Method?.Method ?? "N/A";
                var (host, pathPrefix) = GetUrlInfo(response.RequestMessage?.RequestUri);
                var statusCode = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
                _completedDurationMsMetric.TrackValue(sw.Elapsed.TotalMilliseconds, method, host, pathPrefix, statusCode);

                return response;
            }
            catch (Exception ex)
            {
                try
                {
                    sw.Stop();
                    var method = request.Method?.Method ?? "N/A";
                    var (host, pathPrefix) = GetUrlInfo(request.RequestUri);
                    var exceptionType = ex.GetType().FullName;
                    _exceptionDurationMsMetric.TrackValue(sw.Elapsed.TotalMilliseconds, method, host, pathPrefix, exceptionType);
                }
                catch
                {
                    // best effort
                }

                throw;
            }
        }

        private static readonly char[] StorageSeparators = ['/', '('];

        private static (string Host, string PathPrefix) GetUrlInfo(Uri? uri)
        {
            if (uri is null)
            {
                return ("N/A", "N/A");
            }

            string pathPrefix;
            switch (uri.Host)
            {
                case "127.0.0.1" when uri.Port >= 10000 && uri.Port <= 10002:
                    pathPrefix = GetFirstTwoSegments(uri.AbsolutePath, StorageSeparators);
                    break;
                case "api.nuget.org":
                case "apiint.nugettest.org":
                case "apidev.nugettest.org":
                    pathPrefix = GetV3Segment(uri.AbsolutePath);
                    break;
                case var bh when bh.EndsWith(".blob.core.windows.net", StringComparison.Ordinal):
                case var qh when qh.EndsWith(".queue.core.windows.net", StringComparison.Ordinal):
                case var th when th.EndsWith(".table.core.windows.net", StringComparison.Ordinal):
                    pathPrefix = GetFirstSegment(uri.AbsolutePath, StorageSeparators);
                    break;
                default:
                    pathPrefix = "N/A";
                    break;
            };

            return (uri.Host, pathPrefix);
        }

        private static string GetFirstTwoSegments(string path, char[] secondSeparators)
        {
            var firstIndex = path.IndexOf('/', 1);
            if (firstIndex >= 0)
            {
                var secondIndex = path.IndexOfAny(secondSeparators, firstIndex + 1);
                if (secondIndex >= 0)
                {
                    return Strings.WeakIntern(path.AsSpan(0, secondIndex + 1));
                }
                else
                {
                    return path;
                }
            }

            return "N/A";
        }

        private static string GetFirstSegment(string path, char[] separators)
        {
            var firstIndex = path.IndexOfAny(separators, 1);
            if (firstIndex >= 0)
            {
                return Strings.WeakIntern(path.AsSpan(0, firstIndex + 1));
            }

            return path;
        }

        private static string GetV3Segment(string path)
        {
            switch (path)
            {
                case "/v3/index.json":
                case "/v3/catalog0/index.json":
                case "/v3-flatcontainer/cursor.json":
                    return path;
            }

            var firstIndex = path.IndexOf('/', 1);
            if (firstIndex >= 0)
            {
                var pathSpan = path.AsSpan();
                if (MemoryExtensions.Equals(pathSpan[0..firstIndex], "/v3", StringComparison.Ordinal))
                {
                    var secondIndex = path.IndexOf('/', firstIndex + 1);
                    if (secondIndex >= 0)
                    {
                        return Strings.WeakIntern(path.AsSpan(0, secondIndex + 1));
                    }
                    else
                    {
                        return Strings.WeakIntern(path.AsSpan(0, firstIndex + 1));
                    }
                }
                else
                {
                    return Strings.WeakIntern(path.AsSpan(0, firstIndex + 1));
                }
            }

            return "N/A";
        }
    }
}

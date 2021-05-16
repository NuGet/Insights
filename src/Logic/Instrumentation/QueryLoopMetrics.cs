using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Knapcode.ExplorePackages
{
    public class QueryLoopMetrics : IDisposable
    {
        public const string MetricIdSubstring = ".QueryLoop.";
        private const string DefaultClassName = "UnknownClassName";
        private const string DefaultMethodName = "UnknownMethodName";

        private readonly IMetric _queryDurationMetric;
        private readonly IMetric _queryCountMetric;
        private readonly IMetric _totalDurationMetric;
        private int _queryCount;
        private readonly Stopwatch _totalSw;
        private int _disposed;

        private QueryLoopMetrics(ITelemetryClient telemetryClient, string className, string memberName)
        {
            _queryDurationMetric = telemetryClient.GetMetric($"{className}.{memberName}{MetricIdSubstring}QueryDurationMs");
            _queryCountMetric = telemetryClient.GetMetric($"{className}.{memberName}{MetricIdSubstring}QueryCount");
            _totalDurationMetric = telemetryClient.GetMetric($"{className}.{memberName}{MetricIdSubstring}TotalDurationMs");
            _queryCount = 0;
            _totalSw = Stopwatch.StartNew();
        }

        public static QueryLoopMetrics New(
            ITelemetryClient telemetryClient,
            [CallerFilePath] string sourceFilePath = DefaultClassName,
            [CallerMemberName] string memberName = DefaultMethodName)
        {
            string className = null;
            if (!string.IsNullOrWhiteSpace(sourceFilePath))
            {
                className = Path.GetFileNameWithoutExtension(sourceFilePath);
            }

            if (string.IsNullOrWhiteSpace(className))
            {
                className = DefaultClassName;
            }

            if (string.IsNullOrWhiteSpace(memberName))
            {
                memberName = DefaultMethodName;
            }

            return new QueryLoopMetrics(telemetryClient, className, memberName);
        }

        public IDisposable TrackQuery()
        {
            Interlocked.Increment(ref _queryCount);
            return new QueryDurationMetric(this);
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                _queryCountMetric.TrackValue(_queryCount);
                _totalDurationMetric.TrackValue(_totalSw.Elapsed.TotalMilliseconds);
            }
        }

        private class QueryDurationMetric : IDisposable
        {
            private readonly QueryLoopMetrics _parent;
            private readonly Stopwatch _querySw;
            private int _disposed;

            public QueryDurationMetric(QueryLoopMetrics parent)
            {
                _parent = parent;
                _querySw = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                {
                    _parent._queryDurationMetric.TrackValue(_querySw.Elapsed.TotalMilliseconds);
                }
            }
        }
    }
}

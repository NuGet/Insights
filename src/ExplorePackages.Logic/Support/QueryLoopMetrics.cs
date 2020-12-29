using System;
using System.Diagnostics;

namespace Knapcode.ExplorePackages
{
    public class QueryLoopMetrics : IDisposable
    {
        private readonly IMetric _queryDurationMetric;
        private readonly IMetric _queryCountMetric;
        private readonly IMetric _totalDurationMetric;
        private int _queryCount;
        private readonly Stopwatch _totalSw;

        public QueryLoopMetrics(ITelemetryClient telemetryClient, string className, string methodName)
        {
            _queryDurationMetric = telemetryClient.GetMetric($"{className}.{methodName}.QueryDurationMs");
            _queryCountMetric = telemetryClient.GetMetric($"{className}.{methodName}.QueryCount");
            _totalDurationMetric = telemetryClient.GetMetric($"{className}.{methodName}.TotalDurationMs");
            _queryCount = 0;
            _totalSw = Stopwatch.StartNew();
        }

        public IDisposable TrackQuery()
        {
            _queryCount++;
            return new QueryDurationMetric(this);
        }

        public void Dispose()
        {
            _queryCountMetric.TrackValue(_queryCount);
            _totalDurationMetric.TrackValue(_totalSw.Elapsed.TotalMilliseconds);
        }

        private class QueryDurationMetric : IDisposable
        {
            private readonly QueryLoopMetrics _parent;
            private readonly Stopwatch _querySw;

            public QueryDurationMetric(QueryLoopMetrics parent)
            {
                _parent = parent;
                _querySw = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _parent._queryDurationMetric.TrackValue(_querySw.Elapsed.TotalMilliseconds);
            }
        }
    }
}

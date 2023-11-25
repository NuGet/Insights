// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;

namespace NuGet.Insights
{
    public class QueryLoopMetrics : IDisposable
    {
        public const string MetricIdSubstring = ".QueryLoop.";
        private const string DefaultClassName = "UnknownClassName";
        private const string DefaultMethodName = "UnknownMethodName";

        private readonly Action<double> _queryDurationMetric;
        private readonly Action<double> _queryCountMetric;
        private readonly Action<double> _totalDurationMetric;
        private readonly string[] _dimensionValues;
        private int _queryCount;
        private readonly Stopwatch _totalSw;
        private int _disposed;

        private QueryLoopMetrics(
            ITelemetryClient telemetryClient,
            string className,
            string memberName,
            string[] dimensionNames,
            string[] dimensionValues)
        {
            Action<double> GetMetric(string metricId)
            {
                switch (dimensionNames.Length)
                {
                    case 0:
                        var metric0 = telemetryClient.GetMetric(metricId);
                        return x => metric0.TrackValue(x);
                    case 1:
                        var metric1 = telemetryClient.GetMetric(metricId, dimensionNames[0]);
                        return x => metric1.TrackValue(x, dimensionValues[0]);
                    case 2:
                        var metric2 = telemetryClient.GetMetric(metricId, dimensionNames[0], dimensionNames[1]);
                        return x => metric2.TrackValue(x, dimensionValues[0], dimensionValues[1]);
                    case 3:
                        var metric3 = telemetryClient.GetMetric(metricId, dimensionNames[0], dimensionNames[1], dimensionNames[2]);
                        return x => metric3.TrackValue(x, dimensionValues[0], dimensionValues[1], dimensionValues[2]);
                    case 4:
                        var metric4 = telemetryClient.GetMetric(metricId, dimensionNames[0], dimensionNames[1], dimensionNames[2], dimensionNames[3]);
                        return x => metric4.TrackValue(x, dimensionValues[0], dimensionValues[1], dimensionValues[2], dimensionValues[3]);
                    default:
                        throw new NotImplementedException();
                }
            }

            _queryDurationMetric = GetMetric($"{className}.{memberName}{MetricIdSubstring}QueryDurationMs");
            _queryCountMetric = GetMetric($"{className}.{memberName}{MetricIdSubstring}QueryCount");
            _totalDurationMetric = GetMetric($"{className}.{memberName}{MetricIdSubstring}TotalDurationMs");
            _dimensionValues = dimensionValues;
            _queryCount = 0;
            _totalSw = Stopwatch.StartNew();
        }

        public static QueryLoopMetrics New(
            ITelemetryClient telemetryClient,
            string dimension1Name,
            string dimension1Value,
            [CallerFilePath] string sourceFilePath = DefaultClassName,
            [CallerMemberName] string memberName = DefaultMethodName)
        {
            return New(
                telemetryClient,
                sourceFilePath,
                memberName,
                dimensionNames: new[] { dimension1Name },
                dimensionValues: new[] { dimension1Value } );
        }

        public static QueryLoopMetrics New(
            ITelemetryClient telemetryClient,
            string dimension1Name,
            string dimension1Value,
            string dimension2Name,
            string dimension2Value,
            [CallerFilePath] string sourceFilePath = DefaultClassName,
            [CallerMemberName] string memberName = DefaultMethodName)
        {
            return New(
                telemetryClient,
                sourceFilePath,
                memberName,
                dimensionNames: new[] { dimension1Name, dimension2Name },
                dimensionValues: new[] { dimension1Value, dimension2Value });
        }

        public static QueryLoopMetrics New(
            ITelemetryClient telemetryClient,
            string dimension1Name,
            string dimension1Value,
            string dimension2Name,
            string dimension2Value,
            string dimension3Name,
            string dimension3Value,
            [CallerFilePath] string sourceFilePath = DefaultClassName,
            [CallerMemberName] string memberName = DefaultMethodName)
        {
            return New(
                telemetryClient,
                sourceFilePath,
                memberName,
                dimensionNames: new[] { dimension1Name, dimension2Name, dimension3Name },
                dimensionValues: new[] { dimension1Value, dimension2Value, dimension3Value });
        }

        public static QueryLoopMetrics New(
            ITelemetryClient telemetryClient,
            string dimension1Name,
            string dimension1Value,
            string dimension2Name,
            string dimension2Value,
            string dimension3Name,
            string dimension3Value,
            string dimension4Name,
            string dimension4Value,
            [CallerFilePath] string sourceFilePath = DefaultClassName,
            [CallerMemberName] string memberName = DefaultMethodName)
        {
            return New(
                telemetryClient,
                sourceFilePath,
                memberName,
                dimensionNames: new[] { dimension1Name, dimension2Name, dimension3Name, dimension4Name },
                dimensionValues: new[] { dimension1Value, dimension2Value, dimension3Value, dimension4Value });
        }

        public static QueryLoopMetrics New(
            ITelemetryClient telemetryClient,
            [CallerFilePath] string sourceFilePath = DefaultClassName,
            [CallerMemberName] string memberName = DefaultMethodName)
        {
            return New(
                telemetryClient,
                sourceFilePath,
                memberName,
                dimensionNames: Array.Empty<string>(),
                dimensionValues: Array.Empty<string>());
        }

        private static QueryLoopMetrics New(
            ITelemetryClient telemetryClient,
            string sourceFilePath,
            string memberName,
            string[] dimensionNames,
            string[] dimensionValues)
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

            return new QueryLoopMetrics(telemetryClient, className, memberName, dimensionNames, dimensionValues);
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
                _queryCountMetric(_queryCount);
                _totalDurationMetric(_totalSw.Elapsed.TotalMilliseconds);
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
                    _parent._queryDurationMetric(_querySw.Elapsed.TotalMilliseconds);
                }
            }
        }
    }
}

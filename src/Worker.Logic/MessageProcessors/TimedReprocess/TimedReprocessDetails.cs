// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public record TimedReprocessDetails(
        TimeSpan Window,
        int MaxBuckets,
        DateTimeOffset Now,
        DateTimeOffset WindowStart,
        DateTimeOffset WindowEnd,
        TimeSpan BucketSize,
        int CurrentBucket)
    {
        public static TimedReprocessDetails Create(NuGetInsightsWorkerSettings settings)
        {
            return Create(settings.TimedReprocessWindow, settings.TimedReprocessMaxBuckets);
        }

        public static TimedReprocessDetails Create(TimeSpan window, int maxBuckets)
        {
            var now = DateTimeOffset.UtcNow;

            var windowStart = new DateTimeOffset(now.Ticks - (now.Ticks % window.Ticks), TimeSpan.Zero);
            var windowEnd = windowStart + window;
            var bucketSize = window / BucketedPackage.BucketCount;
            var currentBucket = (int)((now - windowStart) / bucketSize);

            return new TimedReprocessDetails(window, maxBuckets, now, windowStart, windowEnd, bucketSize, currentBucket);
        }
    }
}

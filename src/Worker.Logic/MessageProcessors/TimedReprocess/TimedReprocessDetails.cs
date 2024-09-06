// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker.TimedReprocess
{
    [DebuggerDisplay("{WindowStart} - {WindowEnd} ({Window}, current time is {Now}, up to bucket {CurrentBucket})")]
    public record TimedReprocessDetails(
        TimeSpan Window,
        int MaxBuckets,
        DateTimeOffset Now,
        DateTimeOffset WindowStart,
        DateTimeOffset WindowEnd,
        TimeSpan BucketSize,
        int CurrentBucket)
    {
        public static TimedReprocessDetails Create(DateTimeOffset now, IOptions<NuGetInsightsWorkerSettings> options)
        {
            var settings = options.Value;
            return Create(now, settings.TimedReprocessWindow, settings.TimedReprocessMaxBuckets);
        }

        public static TimedReprocessDetails Create(DateTimeOffset now, TimeSpan window, int maxBuckets)
        {
            return Create(now, now, window, maxBuckets);
        }

        private static TimedReprocessDetails Create(DateTimeOffset now, DateTimeOffset sample, TimeSpan window, int maxBuckets)
        {
            var windowStart = new DateTimeOffset(sample.Ticks - (sample.Ticks % window.Ticks), TimeSpan.Zero);
            var windowEnd = windowStart + window;
            var bucketSize = window / BucketedPackage.BucketCount;
            var currentBucket = (int)((sample - windowStart) / bucketSize);

            return new TimedReprocessDetails(window, maxBuckets, now, windowStart, windowEnd, bucketSize, currentBucket);
        }

        public TimedReprocessDetails GetPrevious()
        {
            return Create(Now, Now - Window, Window, MaxBuckets);
        }

        public TimedReprocessDetails GetNext()
        {
            return Create(Now, Now + Window, Window, MaxBuckets);
        }

        public TimedReprocessDetails GetBounding(DateTimeOffset sample)
        {
            return Create(Now, sample, Window, MaxBuckets);
        }

        public DateTimeOffset GetScheduledTime(int bucket)
        {
            return WindowStart + TimeSpan.FromTicks(bucket * BucketSize.Ticks);
        }
    }
}

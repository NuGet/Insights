// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.LoadBucketedPackage
{
    public record BucketRange
    {
        public BucketRange(int min, int max)
        {
            if (min < 0 || min >= BucketedPackage.BucketCount)
            {
                throw new ArgumentOutOfRangeException(nameof(min), min, $"The min value must be greater than or equal to 0 and less than {BucketedPackage.BucketCount}.");
            }

            if (max < min || max >= BucketedPackage.BucketCount)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, $"The max value must be greater than or equal to the max value and less than {BucketedPackage.BucketCount}.");
            }

            Min = min;
            Max = max;
        }

        public int Min { get; }
        public int Max { get; }

        public IEnumerable<int> Buckets => Enumerable.Range(Min, (Max - Min) + 1);

        public override string ToString()
        {
            if (Min == Max)
            {
                return Min.ToString(CultureInfo.InvariantCulture);
            }

            return $"{Min}-{Max}";
        }

        public static BucketRange Parse(string value)
        {
            var pieces = value.Split('-', 2);
            var min = pieces[0];
            var max = pieces.Length == 2 ? pieces[1] : pieces[0];
            return new BucketRange(int.Parse(min, CultureInfo.InvariantCulture), int.Parse(max, CultureInfo.InvariantCulture));
        }

        public static IEnumerable<int> ParseBuckets(string bucketRanges)
        {
            foreach (var range in ParseRanges(bucketRanges))
            {
                foreach (var bucket in range.Buckets)
                {
                    yield return bucket;
                }
            }
        }

        public static IEnumerable<BucketRange> ParseRanges(string bucketRanges)
        {
            if (string.IsNullOrEmpty(bucketRanges))
            {
                yield break;
            }

            foreach (var range in bucketRanges.Split(","))
            {
                yield return Parse(range);
            }
        }

        public static string BucketsToRanges(IEnumerable<int> buckets)
        {
            var ranges = new List<BucketRange>();
            int? min = null;
            int? max = null;

            foreach (var bucket in buckets.Order())
            {
                if (bucket < 0 || bucket >= BucketedPackage.BucketCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(buckets), bucket, $"The bucket indices must be greater than or equal to 0 and less than {BucketedPackage.BucketCount}.");
                }

                if (!min.HasValue)
                {
                    min = bucket;
                    max = bucket;
                }
                else if (max.Value + 1 == bucket)
                {
                    max = bucket;
                }
                else
                {
                    ranges.Add(new BucketRange(min.Value, max.Value));
                    min = bucket;
                    max = bucket;
                }
            }

            if (min.HasValue)
            {
                ranges.Add(new BucketRange(min.Value, max.Value));
            }

            return ranges.Count > 0 ? string.Join(",", ranges) : null;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System;
using System.Linq;

namespace NuGet.Insights.Worker.LoadBucketedPackage
{
    public record BucketRange(int Min, int Max)
    {
        public override string ToString()
        {
            if (Min == Max)
            {
                return Min.ToString();
            }

            return $"{Min}-{Max}";
        }

        public static BucketRange Parse(string value)
        {
            var pieces = value.Split('-', 2);
            var min = pieces[0];
            var max = pieces.Length == 2 ? pieces[1] : pieces[0];
            return new BucketRange(int.Parse(min), int.Parse(max));
        }

        public static IEnumerable<BucketRange> ParseSequence(string bucketRanges)
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

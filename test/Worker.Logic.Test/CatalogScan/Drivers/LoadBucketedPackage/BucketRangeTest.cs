// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Xunit;

namespace NuGet.Insights.Worker.LoadBucketedPackage
{
    public class BucketRangeTest
    {
        public class Constructor
        {
            [Theory]
            [InlineData(-1)]
            [InlineData(1000)]
            public void RejectsOutOfRangeMin(int min)
            {
                var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new BucketRange(min, min));
                Assert.Equal("min", ex.ParamName);
            }

            [Theory]
            [InlineData(0, -1)]
            [InlineData(0, 1000)]
            [InlineData(100, 50)]
            public void RejectsOutOfRangeMax(int min, int max)
            {
                var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new BucketRange(min, max));
                Assert.Equal("max", ex.ParamName);
            }
        }

        public class Buckets : BucketRangeTest
        {
            [Fact]
            public void SingleBucket()
            {
                var target = new BucketRange(42, 42);

                Assert.Equal(new[] { 42 }, target.Buckets);
            }

            [Fact]
            public void TwoBuckets()
            {
                var target = new BucketRange(42, 43);

                Assert.Equal(new[] { 42, 43 }, target.Buckets);
            }

            [Fact]
            public void ThreeBuckets()
            {
                var target = new BucketRange(42, 44);

                Assert.Equal(new[] { 42, 43, 44 }, target.Buckets);
            }
        }

        public class ParseBuckets : BucketRangeTest
        {
            [Fact]
            public void EmptyString()
            {
                Assert.Empty(BucketRange.ParseBuckets(""));
            }

            [Fact]
            public void ConsecutiveIndices()
            {
                Assert.Equal(
                    new[] { 1, 2, 3 },
                    BucketRange.ParseBuckets("1-3"));
            }

            [Fact]
            public void MixOfRangesAndSingleIndices()
            {
                Assert.Equal(
                    new[] { 1, 2, 3, 23, 42, 43, 44, 45 },
                    BucketRange.ParseBuckets("1-3,23,42-45"));
            }
        }

        public class ParseRanges : BucketRangeTest
        {
            [Fact]
            public void EmptyString()
            {
                Assert.Empty(BucketRange.ParseRanges(""));
            }

            [Fact]
            public void NullString()
            {
                Assert.Empty(BucketRange.ParseRanges(null));
            }

            [Fact]
            public void ConsecutiveIndices()
            {
                Assert.Equal(
                    new[] { 1..1, 2..2, 3..3 }.Select(ToBucketRange).ToList(),
                    BucketRange.ParseRanges("1,2,3"));
            }

            [Fact]
            public void SingleIndex()
            {
                Assert.Equal(
                    new[] { 42..42 }.Select(ToBucketRange).ToList(),
                    BucketRange.ParseRanges("42"));
            }

            [Fact]
            public void SingleRange()
            {
                Assert.Equal(
                    new[] { 23..42 }.Select(ToBucketRange).ToList(),
                    BucketRange.ParseRanges("23-42"));
            }

            [Fact]
            public void MixOfRangesAndSingleIndices()
            {
                Assert.Equal(
                    new[] { 1..1, 7..8, 67..67, 100..100, 503..505, 888..888, 999..999 }.Select(ToBucketRange).ToList(),
                    BucketRange.ParseRanges("1,7-8,67,100,503-505,888,999"));
            }

            [Fact]
            public void OutOfOrder()
            {
                Assert.Equal(
                    new[] { 100..100, 67..999, 42..42, 23..23 }.Select(ToBucketRange).ToList(),
                    BucketRange.ParseRanges("100,67-999,42,23"));
            }
        }

        public class SetBucketRanges : BucketRangeTest
        {
            [Fact]
            public void EmptySequence()
            {
                var actual = BucketRange.BucketsToRanges(Array.Empty<int>());

                Assert.Null(actual);
            }

            [Fact]
            public void NoConsecutiveIndices()
            {
                var actual = BucketRange.BucketsToRanges(new[] { 888, 67, 1, 100, 999 });

                Assert.Equal("1,67,100,888,999", actual);
            }

            [Fact]
            public void MixOfRangesAndSingleIndices()
            {
                var actual = BucketRange.BucketsToRanges(new[] { 888, 67, 1, 7, 8, 100, 505, 504, 503, 999 });

                Assert.Equal("1,7-8,67,100,503-505,888,999", actual);
            }

            [Fact]
            public void SingleIndex()
            {
                var actual = BucketRange.BucketsToRanges(new[] { 42 });

                Assert.Equal("42", actual);
            }

            [Fact]
            public void SingleRange()
            {
                var actual = BucketRange.BucketsToRanges(new[] { 44, 43, 42, 45 });

                Assert.Equal("42-45", actual);
            }

            [Fact]
            public void AllBuckets()
            {
                var actual = BucketRange.BucketsToRanges(Enumerable.Range(0, 1000));

                Assert.Equal("0-999", actual);
            }

            [Fact]
            public void RejectsNegative()
            {
                var ex = Assert.Throws<ArgumentOutOfRangeException>(() => BucketRange.BucketsToRanges(new[] { 1, -1 }));

                Assert.Equal(-1, ex.ActualValue);
            }

            [Fact]
            public void Rejects1000()
            {
                var ex = Assert.Throws<ArgumentOutOfRangeException>(() => BucketRange.BucketsToRanges(new[] { 1, 1000 }));

                Assert.Equal(1000, ex.ActualValue);
            }

            [Fact]
            public void RejectsGreaterThan1000()
            {
                var ex = Assert.Throws<ArgumentOutOfRangeException>(() => BucketRange.BucketsToRanges(new[] { 1, 1001 }));

                Assert.Equal(1001, ex.ActualValue);
            }
        }

        public BucketRange ToBucketRange(Range range)
        {
            return new BucketRange(range.Start.Value, range.End.Value);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class EnumerableExtensionsTest
    {
        public class MergedSorted
        {
            [Fact]
            public void SingleSequence()
            {
                var input = new int[][] { [1, 2, 3, 4, 5] };
                var expected = MergeSimple(input);

                var actual = input.MergedSorted(x => x);

                Assert.Equal(expected, actual.ToArray());
            }

            [Fact]
            public void TwoSequences_SameLength()
            {
                var input = new int[][] { [1, 3, 5, 7, 9], [0, 2, 4, 6, 8] };
                var expected = MergeSimple(input);

                var actual = input.MergedSorted(x => x);

                Assert.Equal(expected, actual.ToArray());
            }

            [Fact]
            public void TwoSequences_DifferentLength()
            {
                var input = new int[][] { [1, 3, 5], [0, 2, 4, 6, 8] };
                var expected = MergeSimple(input);

                var actual = input.MergedSorted(x => x);

                Assert.Equal(expected, actual.ToArray());
            }

            [Fact]
            public void TwoSequences_Intersection()
            {
                var input = new int[][] { [0, 2, 4, 6, 8], [0, 2, 4, 6, 8] };
                var expected = MergeSimple(input);

                var actual = input.MergedSorted(x => x);

                Assert.Equal(expected, actual.ToArray());
            }

            [Fact]
            public void TwoSequences_Duplicates()
            {
                var input = new int[][] { [1, 1, 1, 1, 3, 5, 7, 9], [0, 2, 2, 4, 6, 8, 8] };
                var expected = MergeSimple(input);

                var actual = input.MergedSorted(x => x);

                Assert.Equal(expected, actual.ToArray());
            }

            [Fact]
            public void TwoSequences_DuplicatesAndIntersection()
            {
                var input = new int[][] { [1, 1, 1, 1, 3, 5, 7, 8, 9], [0, 1, 1, 2, 2, 4, 6, 8, 8] };
                var expected = MergeSimple(input);

                var actual = input.MergedSorted(x => x);

                Assert.Equal(expected, actual.ToArray());
            }

            [Fact]
            public void TwoSequences_WithEmtpy()
            {
                var input = new int[][] { [], [0, 2, 4, 6, 8] };
                var expected = MergeSimple(input);

                var actual = input.MergedSorted(x => x);

                Assert.Equal(expected, actual.ToArray());
            }

            private T[] MergeSimple<T>(IEnumerable<IEnumerable<T>> sources) where T : IComparable<T>
            {
                return sources.SelectMany(x => x).Order().ToArray();
            }
        }
    }
}

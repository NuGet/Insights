// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Insights
{
    public static class IterTools
    {
        public static IEnumerable<List<T>> Interleave<T>(IReadOnlyList<T> s, IReadOnlyList<T> t)
        {
            return Interleave(s, t, new List<T>(), 0, 0);
        }

        /// <summary>
        /// Source: https://stackoverflow.com/a/36261144
        /// </summary>
        private static IEnumerable<List<T>> Interleave<T>(IReadOnlyList<T> s, IReadOnlyList<T> t, List<T> res, int i, int j)
        {
            if (i == s.Count && j == t.Count)
            {
                yield return res;
            }

            if (i < s.Count)
            {
                var next = new List<T>(res) { s[i] };
                foreach (var item in Interleave(s, t, next, i + 1, j))
                {
                    yield return item;
                }
            }

            if (j < t.Count)
            {
                var next = new List<T>(res) { t[j] };
                foreach (var item in Interleave(s, t, next, i, j + 1))
                {
                    yield return item;
                }
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public static class EnumerableExtensions
    {
        public static ILookup<TKey, TValue> ToLookup<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, IEnumerable<TValue>>> dictionary)
        {
            return dictionary
                .SelectMany(p => p.Value, KeyValuePair.Create)
                .ToLookup(p => p.Key.Key, p => p.Value);
        }

        /// <summary>
        /// Source: https://stackoverflow.com/a/2767338
        /// </summary>
        public static IEnumerable<TSource> MergedSorted<TSource, TKey>(this IEnumerable<IEnumerable<TSource>> sources, Func<TSource, TKey> keySelector)
            where TKey : IComparable<TKey>
        {
            List<IEnumerator<TSource>> items = sources
                .Select(source => source.GetEnumerator())
                .Where(enumerator => enumerator.MoveNext())
                .OrderBy(enumerator => keySelector(enumerator.Current))
                .ToList();

            try
            {
                return MergedSorted(items, keySelector);
            }
            catch
            {
                foreach (IEnumerator<TSource> item in items)
                {
                    try
                    {
                        item.Dispose();
                    }
                    catch
                    {
                        // best effort
                    }
                }

                throw;
            }
        }

        private static IEnumerable<TSource> MergedSorted<TSource, TKey>(List<IEnumerator<TSource>> items, Func<TSource, TKey> keySelector)
            where TKey : IComparable<TKey>
        {
            while (items.Count > 0)
            {
                yield return items[0].Current;

                IEnumerator<TSource> next = items[0];
                items.RemoveAt(0);
                if (next.MoveNext())
                {
                    // simple sorted linear insert
                    TKey value = keySelector(next.Current);
                    int i;
                    for (i = 0; i < items.Count; i++)
                    {
                        if (value.CompareTo(keySelector(items[i].Current)) <= 0)
                        {
                            items.Insert(i, next);
                            break;
                        }
                    }

                    if (i == items.Count)
                    {
                        items.Add(next);
                    }
                }
                else
                {
                    next.Dispose();
                }
            }
        }
    }
}

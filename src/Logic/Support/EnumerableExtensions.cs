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
    }
}

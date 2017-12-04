using System.Collections.Generic;
using System.Linq;

namespace Knapcode.ExplorePackages.Support
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

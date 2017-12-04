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
                .SelectMany(pair => pair
                    .Value
                    .Select(value => new
                    {
                        Key = pair.Key,
                        Value = value,
                    }))
                .ToLookup(x => x.Key, x => x.Value);
        }
    }
}

using System.Collections.Generic;

namespace Knapcode.ExplorePackages
{
    public static class KeyValuePairFactory
    {
        public static KeyValuePair<TKey, TValue> Create<TKey, TValue>(TKey key, TValue value)
        {
            return new KeyValuePair<TKey, TValue>(key, value);
        }
    }
}

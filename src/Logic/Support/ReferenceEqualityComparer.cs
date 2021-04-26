using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Knapcode.ExplorePackages
{
    public class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    {
        public static ReferenceEqualityComparer<T> Instance { get; } = new ReferenceEqualityComparer<T>();

        public bool Equals([AllowNull] T x, [AllowNull] T y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode([DisallowNull] T obj)
        {
            return obj.GetHashCode();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NuGet.Insights.Worker
{
    public class PackageRecordIdVersionComparer : IEqualityComparer<PackageRecord>
    {
        public static PackageRecordIdVersionComparer Instance { get; } = new PackageRecordIdVersionComparer();

        public bool Equals([AllowNull] PackageRecord x, [AllowNull] PackageRecord y)
        {
            if (x == null && y == null)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            return StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id)
                && StringComparer.OrdinalIgnoreCase.Equals(x.Version, y.Version);
        }

        public int GetHashCode([DisallowNull] PackageRecord obj)
        {
            var hashCode = new HashCode();
            hashCode.Add(obj.Id, StringComparer.OrdinalIgnoreCase);
            hashCode.Add(obj.Version, StringComparer.OrdinalIgnoreCase);
            return hashCode.ToHashCode();
        }
    }
}

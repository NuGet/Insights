using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Knapcode.ExplorePackages.Worker.FindPackageAssets
{
    public class PackageAssetIdVersionComparer : IEqualityComparer<PackageAsset>
    {
        public static PackageAssetIdVersionComparer Instance { get; } = new PackageAssetIdVersionComparer();

        public bool Equals([AllowNull] PackageAsset x, [AllowNull] PackageAsset y)
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

        public int GetHashCode([DisallowNull] PackageAsset obj)
        {
            var hashCode = new HashCode();
            hashCode.Add(obj.Id, StringComparer.OrdinalIgnoreCase);
            hashCode.Add(obj.Version, StringComparer.OrdinalIgnoreCase);
            return hashCode.ToHashCode();
        }
    }
}

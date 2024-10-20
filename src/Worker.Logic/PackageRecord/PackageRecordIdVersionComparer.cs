// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.Worker
{
    public class PackageRecordIdVersionComparer<T> : IEqualityComparer<T> where T : IPackageRecord
    {
        public static PackageRecordIdVersionComparer<T> Instance { get; } = new();

        public bool Equals(T? x, T? y)
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

        public int GetHashCode([DisallowNull] T obj)
        {
            var hashCode = new HashCode();
            hashCode.Add(obj.Id, StringComparer.OrdinalIgnoreCase);
            hashCode.Add(obj.Version, StringComparer.OrdinalIgnoreCase);
            return hashCode.ToHashCode();
        }
    }
}

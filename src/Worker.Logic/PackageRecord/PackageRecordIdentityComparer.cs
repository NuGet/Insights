// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.Worker
{
    public class PackageRecordIdentityComparer<T> : IEqualityComparer<T> where T : IPackageRecord
    {
        public static PackageRecordIdentityComparer<T> Instance { get; } = new();

        public bool Equals(T? x, T? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.Identity == y.Identity;
        }

        public int GetHashCode([DisallowNull] T obj)
        {
            return obj.Identity.GetHashCode(StringComparison.Ordinal);
        }
    }
}

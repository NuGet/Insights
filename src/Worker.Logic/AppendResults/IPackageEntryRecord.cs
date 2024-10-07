// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.Worker
{
    public interface IPackageEntryRecord
    {
        string Identity { get; }
        int? SequenceNumber { get; }

        public class PackageEntryKeyComparer<T> : IEqualityComparer<T> where T : IPackageEntryRecord
        {
            public static PackageEntryKeyComparer<T> Instance { get; } = new();

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

                return x.SequenceNumber == y.SequenceNumber
                    && x.Identity == y.Identity;
            }

            public int GetHashCode([DisallowNull] T obj)
            {
                var hashCode = new HashCode();
                hashCode.Add(obj.Identity);
                hashCode.Add(obj.SequenceNumber);
                return hashCode.ToHashCode();
            }
        }
    }
}

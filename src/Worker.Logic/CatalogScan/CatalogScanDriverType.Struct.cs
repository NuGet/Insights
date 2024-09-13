// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Frozen;
using System.ComponentModel;

namespace NuGet.Insights.Worker
{
    [TypeConverter(typeof(CatalogScanDriverTypeConverter))]
    public partial struct CatalogScanDriverType : IEquatable<CatalogScanDriverType>, IComparable<CatalogScanDriverType>
    {
        private static readonly FrozenDictionary<string, CatalogScanDriverType> NameToDriverType = typeof(CatalogScanDriverType)
            .GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.GetProperty)
            .Where(x => x.PropertyType == typeof(CatalogScanDriverType))
            .Select(x => (CatalogScanDriverType)x.GetValue(null)!)
            .ToDictionary(x => x.ToString())
            .ToFrozenDictionary();

        public static IReadOnlyList<CatalogScanDriverType> AllTypes { get; } = NameToDriverType.Values.Order().ToList();

        public static bool TryParse(string? name, out CatalogScanDriverType driverType)
        {
            if (name is null)
            {
                driverType = default;
                return false;
            }

            return NameToDriverType.TryGetValue(name, out driverType);
        }

        public static CatalogScanDriverType Parse(string? name)
        {
            if (TryParse(name, out var driverType))
            {
                return driverType;
            }

            throw new ArgumentException($"The driver type '{name}' is not supported.", nameof(name));
        }

        private readonly string? _name;

        public CatalogScanDriverType(string name)
        {
            _name = name;
        }

        public override string ToString()
        {
            return _name ?? "<uninitialized>";
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is CatalogScanDriverType c && Equals(c);
        }

        public bool Equals(CatalogScanDriverType other)
        {
            return other._name == _name;
        }

        public int CompareTo(CatalogScanDriverType other)
        {
            return string.CompareOrdinal(_name, other._name);
        }

        public override int GetHashCode()
        {
            if (_name is null)
            {
                return 0;
            }

            return StringComparer.Ordinal.GetHashCode(_name);
        }

        public static bool operator ==(CatalogScanDriverType x, CatalogScanDriverType y)
        {
            return x._name == y._name;
        }

        public static bool operator !=(CatalogScanDriverType x, CatalogScanDriverType y)
        {
            return x._name != y._name;
        }

        public static bool operator <(CatalogScanDriverType left, CatalogScanDriverType right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(CatalogScanDriverType left, CatalogScanDriverType right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(CatalogScanDriverType left, CatalogScanDriverType right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(CatalogScanDriverType left, CatalogScanDriverType right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}

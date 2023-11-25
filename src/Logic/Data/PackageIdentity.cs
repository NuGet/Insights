// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackageIdentity : IEquatable<PackageIdentity>
    {
        private readonly int _hashCode;

        public PackageIdentity(string id, string version)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Value = $"{Id}/{Version}";
            _hashCode = StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
        }

        public string Id { get; }
        public string Version { get; }
        public string Value { get; }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PackageIdentity);
        }

        public bool Equals(PackageIdentity other)
        {
            if (other == null)
            {
                return false;
            }

            return StringComparer.OrdinalIgnoreCase.Equals(Id, other.Id)
                && StringComparer.OrdinalIgnoreCase.Equals(Version, other.Version);
        }
    }
}

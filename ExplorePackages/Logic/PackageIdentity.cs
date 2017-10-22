using System;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageIdentity : IEquatable<PackageIdentity>
    {
        public PackageIdentity(string id, string version)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Version = version ?? throw new ArgumentNullException(nameof(version));
        }

        public string Id { get; }
        public string Version { get; }

        public override string ToString()
        {
            return $"{Id}/{Version}";
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Id) * 17
                + StringComparer.OrdinalIgnoreCase.GetHashCode(Version);
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

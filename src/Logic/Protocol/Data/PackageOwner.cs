using System;

namespace NuGet.Insights
{
    public class PackageOwner : IEquatable<PackageOwner>
    {
        public PackageOwner(string id, string username)
        {
            Id = id;
            Username = username;
        }

        public string Id { get; }
        public string Username { get; }

        public override bool Equals(object obj)
        {
            return Equals(obj as PackageOwner);
        }

        public bool Equals(PackageOwner other)
        {
            return other != null &&
                StringComparer.OrdinalIgnoreCase.Equals(Id, other.Id) &&
                StringComparer.OrdinalIgnoreCase.Equals(Username, other.Username);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Id, StringComparer.OrdinalIgnoreCase);
            hashCode.Add(Username, StringComparer.OrdinalIgnoreCase);
            return hashCode.ToHashCode();
        }
    }
}

using System;
using System.Collections.Generic;

namespace NuGet.Insights
{
    public class PackageDownloads : IEquatable<PackageDownloads>
    {
        public PackageDownloads(string id, string version, long downloads)
        {
            Id = id;
            Version = version;
            Downloads = downloads;
        }

        public string Id { get; }
        public string Version { get; }
        public long Downloads { get; }

        public bool Equals(PackageDownloads other)
        {
            return other != null &&
                   Id == other.Id &&
                   Version == other.Version &&
                   Downloads == other.Downloads;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PackageDownloads);
        }

        public override int GetHashCode()
        {
            var hashCode = -710329939;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Id);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Version);
            hashCode = hashCode * -1521134295 + Downloads.GetHashCode();
            return hashCode;
        }

    }
}

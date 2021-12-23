// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            return HashCode.Combine(Id, Version, Downloads);
        }
    }
}

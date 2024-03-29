// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class ExcludedPackage : IEquatable<ExcludedPackage>
    {
        public ExcludedPackage(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public override bool Equals(object obj)
        {
            return Equals(obj as ExcludedPackage);
        }

        public bool Equals(ExcludedPackage other)
        {
            return other != null &&
                StringComparer.OrdinalIgnoreCase.Equals(Id, other.Id);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Id, StringComparer.OrdinalIgnoreCase);
            return hashCode.ToHashCode();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PopularityTransfer
    {
        public PopularityTransfer(string fromId, string toId)
        {
            FromId = fromId;
            ToId = toId;
        }

        public string FromId { get; }
        public string ToId { get; }

        public override bool Equals(object obj)
        {
            return Equals(obj as PopularityTransfer);
        }

        public bool Equals(PopularityTransfer other)
        {
            return other != null &&
                StringComparer.OrdinalIgnoreCase.Equals(FromId, other.FromId) &&
                StringComparer.OrdinalIgnoreCase.Equals(ToId, other.ToId);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(FromId, StringComparer.OrdinalIgnoreCase);
            hashCode.Add(ToId, StringComparer.OrdinalIgnoreCase);
            return hashCode.ToHashCode();
        }
    }
}

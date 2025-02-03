// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PopularityTransfer
    {
        public PopularityTransfer(string id, string transferId)
        {
            Id = id;
            TransferId = transferId;
        }

        public string Id { get; }
        public string TransferId { get; }

        public override bool Equals(object obj)
        {
            return Equals(obj as PopularityTransfer);
        }

        public bool Equals(PopularityTransfer other)
        {
            return other != null &&
                StringComparer.OrdinalIgnoreCase.Equals(Id, other.Id) &&
                StringComparer.OrdinalIgnoreCase.Equals(TransferId, other.TransferId);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Id, StringComparer.OrdinalIgnoreCase);
            hashCode.Add(TransferId, StringComparer.OrdinalIgnoreCase);
            return hashCode.ToHashCode();
        }
    }
}

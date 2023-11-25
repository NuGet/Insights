// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;

namespace NuGet.Insights.ReferenceTracking
{
    [MessagePackObject]
    public class OwnerToSubjectEdges : IEquatable<OwnerToSubjectEdges>
    {
        [Key(0)]
        public IReadOnlyList<SubjectEdge> Committed { get; set; }
        [Key(1)]
        public IReadOnlyList<SubjectReference> ToAdd { get; set; }
        [Key(2)]
        public IReadOnlyList<SubjectReference> ToDelete { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as OwnerToSubjectEdges);
        }

        public bool Equals(OwnerToSubjectEdges other)
        {
            return other != null &&
                   Committed.SequenceEqual(other.Committed) &&
                   ToAdd.SequenceEqual(other.ToAdd) &&
                   ToDelete.SequenceEqual(other.ToDelete);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();

            foreach (var subjectEdge in Committed)
            {
                hashCode.Add(subjectEdge);
            }

            foreach (var subjectEdge in ToAdd)
            {
                hashCode.Add(subjectEdge);
            }

            foreach (var subjectEdge in ToDelete)
            {
                hashCode.Add(subjectEdge);
            }

            return hashCode.ToHashCode();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.ReferenceTracking;

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public interface ICleanupOrphanRecordsAdapter<T> where T : ICleanupOrphanCsvRecord
    {
        string OwnerToSubjectTableName { get; }
        string SubjectToOwnerTableName { get; }
        string OperationName { get; }
        string OwnerType { get; }
        string SubjectType { get; }
        IReadOnlyList<T> MapToOrphanRecords(IReadOnlyList<SubjectReference> subjects);
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Insights.ReferenceTracking;

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class TestCleanupOrphanRecordsAdapter : ICleanupOrphanRecordsAdapter<TestSubjectRecord>
    {
        public string OwnerType => "O";
        public string SubjectType => "S";

        public IReadOnlyList<ICsvRecordSet<TestSubjectRecord>> MapToOrphanRecords(IReadOnlyList<SubjectReference> subjects)
        {
            return subjects
                .GroupBy(subject => subject.PartitionKey)
                .Select(group => new CsvRecordSet<TestSubjectRecord>(
                    group.Key,
                    group
                        .Select(subject => new TestSubjectRecord { BucketKey = group.Key, Id = subject.RowKey, IsOrphan = true })
                        .ToList()))
                .ToList();
        }
    }
}

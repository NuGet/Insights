// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.ReferenceTracking;

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class TestCleanupOrphanRecordsAdapter : ICleanupOrphanRecordsAdapter<TestSubjectRecord>
    {
        private readonly CleanupOrphanRecordsServiceTest _test;

        public TestCleanupOrphanRecordsAdapter(CleanupOrphanRecordsServiceTest test)
        {
            _test = test;
        }

        public string OperationName => "CleanupTestSubjectRecords";
        public string OwnerType => "O";
        public string SubjectType => "S";
        public string OwnerToSubjectTableName => _test.OwnerToSubjectTableName;
        public string SubjectToOwnerTableName => _test.SubjectToOwnerTableName;

        public IReadOnlyList<TestSubjectRecord> MapToOrphanRecords(IReadOnlyList<SubjectReference> subjects)
        {
            return subjects
                .Select(subject => new TestSubjectRecord { BucketKey = subject.PartitionKey, Id = subject.RowKey, IsOrphan = true })
                .ToList();
        }
    }
}

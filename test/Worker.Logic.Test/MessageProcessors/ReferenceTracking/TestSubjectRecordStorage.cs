// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class TestSubjectRecordStorage : ICsvResultStorage<TestSubjectRecord>
    {
        private readonly CleanupOrphanRecordsServiceTest _test;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public TestSubjectRecordStorage(
            CleanupOrphanRecordsServiceTest test,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _test = test;
            _options = options;
        }

        public string ResultContainerName => _test.ResultContainerName;

        public List<TestSubjectRecord> Prune(List<TestSubjectRecord> records, bool isFinalPrune)
        {
            return records
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .Where(x => !isFinalPrune || !x.IsOrphan)
                .ToList();
        }
    }
}

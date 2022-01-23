// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class TestSubjectRecordStorage : ICsvResultStorage<TestSubjectRecord>
    {
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public TestSubjectRecordStorage(IOptions<NuGetInsightsWorkerSettings> options)
        {
            _options = options;
        }

        public string ResultContainerName => _options.Value.CatalogLeafItemContainerName;

        public Task<ICatalogLeafItem> MakeReprocessItemOrNullAsync(TestSubjectRecord record)
        {
            throw new NotImplementedException();
        }

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

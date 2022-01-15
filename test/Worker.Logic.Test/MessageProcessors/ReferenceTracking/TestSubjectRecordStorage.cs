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

        public List<TestSubjectRecord> Prune(List<TestSubjectRecord> records)
        {
            return records
                .GroupBy(x => x.Id)
                .Where(g => g.All(x => !x.IsOrphan))
                .Select(g => g.First())
                .ToList();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace NuGet.Insights.Worker
{
    public class PackageRecordFacts
    {
        public class Prune
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void RemovesExactDuplicates(bool isFinalPrune)
            {
                Func<PackageRecord> getRecord = () => new PackageRecord
                {
                    ScanId = Guid.Parse("7102d763-b2ba-46cf-a27b-0c9cbefe1ce8"),
                    CatalogCommitTimestamp = new DateTimeOffset(2023, 10, 17, 22, 10, 13, TimeSpan.Zero),
                    Created = new DateTimeOffset(2023, 10, 15, 0, 0, 0, TimeSpan.Zero),
                    Id = "NuGet.Versioning",
                    Identity = "nuget.versioning/6.0.0",
                    LowerId = "nuget.versioning",
                    ScanTimestamp = new DateTimeOffset(2023, 10, 17, 22, 11, 54, TimeSpan.Zero),
                    Version = "6.0.0",
                };
                var a = getRecord();
                var b = getRecord();
                var records = new List<PackageRecord> { a, b };

                var pruned = PackageRecord.Prune(records, isFinalPrune);

                var single = Assert.Single(pruned);
                Assert.Equal(a, single);
            }
        }
    }
}

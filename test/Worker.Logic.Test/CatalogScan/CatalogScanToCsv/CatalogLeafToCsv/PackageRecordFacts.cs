// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.PackageVersionToCsv;

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
                Func<PackageVersionRecord> getRecord = () => new PackageVersionRecord
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
                var records = new List<PackageVersionRecord> { a, b };

                var pruned = PackageRecord.Prune(records, isFinalPrune);

                var single = Assert.Single(pruned);
                Assert.Equal(a, single);
            }
        }

        public class CompareTo
        {
            [Theory]
            [InlineData("Ab", "1.0.0", "Ab", "1.0.0", 0)]
            [InlineData("Ab", "1.0.0", "Ab", "1.0", 0)]
            [InlineData("Ab", "1.0.0", "Ab", "1", 0)]
            [InlineData("ab", "1.0.0", "AB", "1.0.0", 0)]
            [InlineData("Ab", "1.0.0-beta.1", "Ab", "1.0.0-beta.1", 0)]
            [InlineData("Ab", "1.0.0-BETA.A", "Ab", "1.0.0-beta.a", 0)]
            [InlineData("ab", "1.0.0-beta", "AB", "1.0.0-BETA", 0)]
            [InlineData("ab", "1.0.0-beta", "ab", "1.0.0-BETA", 0)]
            [InlineData("ab", "1.0.0.0-beta", "ab", "1.0.0-BETA", 0)]
            [InlineData("ab", "1.0.00-beta", "ab", "1.0.0-BETA", 0)]
            [InlineData("ab", "1.0.01-beta", "ab", "1.0.1-BETA", 0)]
            [InlineData("Ab", "2.0.0", "Ab", "1.0.0", 1)]
            [InlineData("abc", "1.0.0", "ab", "1.0.0", 1)]
            [InlineData("abc", "1.0.0-beta.2", "abc", "1.0.0-beta.1", 1)]
            [InlineData("abc", "1.0.0-beta.a", "abc", "1.0.0-beta.1", 1)]
            [InlineData("abc", "1.0.0-beta", "abc", "1.0.0", 1)]
            [InlineData("abc", "1.0.0", "ab", "1.0.0-beta", 1)]
            [InlineData("abc", "1.0.0-beta", "abc", "1.0.0-alpha", 1)]
            [InlineData("abc", "1.0.0-beta", "abc", "1.0.0-bet", 1)]
            [InlineData("abc", "1.0.0-beta", "abc", "1.0.0-bet0", 1)]
            [InlineData("abc", "1.0.0-betz", "abc", "1.0.0-beta", 1)]
            [InlineData("z", "1.0.0", "a", "1.0.0", 1)]
            [InlineData("z", "1.0.0", "a", "2.0.0", 1)]
            [InlineData("zz", "1.0.0-beta", "z", "1.0.0", 1)]
            [InlineData("a.b.c", "1.0.0", "a.b", "1.0.0", 1)]
            [InlineData("a_b_c", "1.0.0", "a_b", "1.0.0", 1)]
            [InlineData("a-b-c", "1.0.0", "a-b", "1.0.0", 1)]
            public void ComparesIdAndVersion(string idA, string vA, string idB, string vB, int expected)
            {
                var a = GetPackagRecord(idA, vA);
                var b = GetPackagRecord(idB, vB);

                var actual = a.CompareTo(b);
                var reverse = b.CompareTo(a);

                Assert.Equal(expected, Clamp(actual));
                Assert.Equal(expected * -1, Clamp(reverse));
                Assert.Equal(expected, Clamp((a.LowerId, a.Version.ToLowerInvariant()).CompareTo((b.LowerId, b.Version.ToLowerInvariant()))));
            }

            private static int Clamp(int actual)
            {
                return Math.Clamp(actual, -1, 1);
            }

            private static PackageRecord GetPackagRecord(string id, string version)
            {
                var lowerId = id.ToLowerInvariant();
                var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
                return new PackageRecord
                {
                    LowerId = lowerId,
                    Identity = PackageRecord.GetIdentity(lowerId, normalizedVersion),
                    Id = id,
                    Version = normalizedVersion,
                };
            }
        }
    }
}

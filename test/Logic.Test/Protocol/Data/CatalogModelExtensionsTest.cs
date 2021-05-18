// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Insights
{
    public class CatalogModelExtensionsTest
    {
        public class GetSemVerType
        {
            [Theory]
            [InlineData("1.0.0", null, SemVerType.SemVer1)]
            [InlineData("1.0.0-beta", null, SemVerType.SemVer1)]
            [InlineData("1.0.0", "1.0.0", SemVerType.SemVer1)]
            [InlineData("1.0.0", "1.0.0-beta", SemVerType.SemVer1)]
            [InlineData("1.0.0-beta.1", null, SemVerType.VersionHasPrereleaseDots)]
            [InlineData("1.0.0+git", null, SemVerType.VersionHasBuildMetadata)]
            [InlineData("1.0.0-beta.1+git", null, SemVerType.VersionHasPrereleaseDots | SemVerType.VersionHasBuildMetadata)]
            [InlineData("1.0.0", "1.0.0-beta.1", SemVerType.DependencyMinHasPrereleaseDots)]
            [InlineData("1.0.0", "1.0.0+git", SemVerType.DependencyMinHasBuildMetadata)]
            [InlineData("1.0.0", "1.0.0-beta.1+git", SemVerType.DependencyMinHasPrereleaseDots | SemVerType.DependencyMinHasBuildMetadata)]
            [InlineData("1.0.0", "(, 1.0.0-beta.1]", SemVerType.DependencyMaxHasPrereleaseDots)]
            [InlineData("1.0.0", "(, 1.0.0+git]", SemVerType.DependencyMaxHasBuildMetadata)]
            [InlineData("1.0.0", "(, 1.0.0-beta.1+git]", SemVerType.DependencyMaxHasPrereleaseDots | SemVerType.DependencyMaxHasBuildMetadata)]
            [InlineData("1.0.0", "[1.0.0+git, 2.0.0-beta.1]", SemVerType.DependencyMinHasBuildMetadata | SemVerType.DependencyMaxHasPrereleaseDots)]
            [InlineData("1.0.0", "[1.0.0+git, 2.0.0+git]", SemVerType.DependencyMinHasBuildMetadata | SemVerType.DependencyMaxHasBuildMetadata)]
            [InlineData("1.0.0", "[1.0.0+git, 2.0.0-beta.1+git]", SemVerType.DependencyMinHasBuildMetadata | SemVerType.DependencyMaxHasPrereleaseDots | SemVerType.DependencyMaxHasBuildMetadata)]
            [InlineData("1.0.0-beta.1", "[1.0.0+git, 2.0.0-beta.1+git]", SemVerType.VersionHasPrereleaseDots | SemVerType.DependencyMinHasBuildMetadata | SemVerType.DependencyMaxHasPrereleaseDots | SemVerType.DependencyMaxHasBuildMetadata)]
            public void ReturnsExpected(string packageVersion, string dependencyVersionRange, SemVerType expected)
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageVersion = packageVersion,
                };

                if (dependencyVersionRange != null)
                {
                    leaf.DependencyGroups = new List<CatalogPackageDependencyGroup>
                    {
                        new CatalogPackageDependencyGroup
                        {
                            Dependencies = new List<CatalogPackageDependency>
                            {
                                new CatalogPackageDependency
                                {
                                    Range = dependencyVersionRange,
                                },
                            },
                        },
                    };
                }

                var actual = leaf.GetSemVerType();

                Assert.Equal(expected, actual);
                if (expected != SemVerType.SemVer1)
                {
                    Assert.NotEqual((SemVerType)0, actual & SemVerType.SemVer2);
                }
            }

            [Theory]
            [MemberData(nameof(AllSemVerTypes))]
            public void SemVerTypeExpectedValues(SemVerType semVerType)
            {
                Assert.Contains(semVerType, SemVerTypeValues.Keys);
                Assert.Equal(SemVerTypeValues[semVerType], (int)semVerType);
            }

            public static IEnumerable<object[]> AllSemVerTypes => Enum
                .GetValues(typeof(SemVerType))
                .Cast<SemVerType>()
                .Select(x => new object[] { x });

            private static readonly Dictionary<SemVerType, int> SemVerTypeValues = new Dictionary<SemVerType, int>
            {
                { SemVerType.SemVer1, 0 },
                { SemVerType.VersionHasPrereleaseDots, 1 },
                { SemVerType.VersionHasBuildMetadata, 2 },
                { SemVerType.DependencyMinHasPrereleaseDots, 4 },
                { SemVerType.DependencyMinHasBuildMetadata, 8 },
                { SemVerType.DependencyMaxHasPrereleaseDots, 16 },
                { SemVerType.DependencyMaxHasBuildMetadata, 32 },
                { SemVerType.SemVer2, 63 },
            };
        }
    }
}

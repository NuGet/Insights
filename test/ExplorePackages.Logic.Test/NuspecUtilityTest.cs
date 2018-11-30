using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NuGet.Frameworks;
using Xunit;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecUtilityTest
    {
        [Fact]
        public void HasCollidingMetadataElements()
        {
            // Arrange
            var nuspec = Resources.LoadXml(Resources.Nuspecs.CollidingMetadataElements);

            // Act
            var output = NuspecUtility.GetCollidingMetadataElements(nuspec);

            // Assert
            Assert.Equal(
                new[]
                {
                    "created", "dependencyGroups", "isPrerelease", "lastEdited", "listed", "packageEntries",
                    "packageHash", "packageHashAlgorithm", "packageSize", "published", "supportedFrameworks",
                    "verbatimVersion",
                },
                output);
        }

        [Fact]
        public void GetUnexpectedValuesForBooleanMetadata()
        {
            // Arrange
            var nuspec = Resources.LoadXml(Resources.Nuspecs.UnexpectedValuesForBooleans);

            // Act
            var output = NuspecUtility.GetUnexpectedValuesForBooleanMetadata(nuspec);

            // Assert
            Assert.Equal(2, output.Count);
            Assert.Equal(output["requireLicenseAcceptance"], new[] { "0", "1" });
            Assert.Equal(output["serviceable"], new[] { string.Empty, string.Empty });
        }

        [Fact]
        public void HasDuplicateMetadataElementsCaseSensitive()
        {
            // Arrange
            var nuspec = Resources.LoadXml(Resources.Nuspecs.DuplicateMetadataElements);

            // Act
            var output = NuspecUtility.GetDuplicateMetadataElements(nuspec, caseSensitive: true, onlyText: false);

            // Assert
            Assert.NotEmpty(output);
            Assert.Equal(new[] { "authors", "dependencies", "owners" }, output.Keys.OrderBy(x => x));
            Assert.Equal(3, output["authors"]);
            Assert.Equal(2, output["dependencies"]);
            Assert.Equal(2, output["owners"]);
        }

        [Fact]
        public void HasDuplicateTextMetadataElementsCaseSensitive()
        {
            // Arrange
            var nuspec = Resources.LoadXml(Resources.Nuspecs.DuplicateMetadataElements);

            // Act
            var output = NuspecUtility.GetDuplicateMetadataElements(nuspec, caseSensitive: true, onlyText: true);

            // Assert
            Assert.NotEmpty(output);
            Assert.Equal(new[] { "authors" }, output.Keys.OrderBy(x => x));
            Assert.Equal(3, output["authors"]);
        }

        [Fact]
        public void HasDuplicateMetadataElementsCaseInsensitive()
        {
            // Arrange
            var nuspec = Resources.LoadXml(Resources.Nuspecs.DuplicateMetadataElements);

            // Act
            var output = NuspecUtility.GetDuplicateMetadataElements(nuspec, caseSensitive: false, onlyText: false);

            // Assert
            Assert.NotEmpty(output);
            Assert.Equal(new[] { "authors", "dependencies", "description", "owners" }, output.Keys.OrderBy(x => x));
            Assert.Equal(3, output["authors"]);
            Assert.Equal(2, output["dependencies"]);
            Assert.Equal(3, output["description"]);
            Assert.Equal(2, output["owners"]);
        }

        [Fact]
        public void HasDuplicateTextMetadataElementsCaseInsensitive()
        {
            // Arrange
            var nuspec = Resources.LoadXml(Resources.Nuspecs.DuplicateMetadataElements);

            // Act
            var output = NuspecUtility.GetDuplicateMetadataElements(nuspec, caseSensitive: false, onlyText: true);

            // Assert
            Assert.NotEmpty(output);
            Assert.Equal(new[] { "authors", "description" }, output.Keys.OrderBy(x => x));
            Assert.Equal(3, output["authors"]);
            Assert.Equal(3, output["description"]);
        }

        [Fact]
        public void HasNonAlphabetMetadataElements()
        {
            // Arrange
            var nuspec = Resources.LoadXml(Resources.Nuspecs.NonAlphabetMetadataElements);

            // Act
            var output = NuspecUtility.GetNonAlphabetMetadataElements(nuspec);

            // Assert
            Assert.Equal(
                new[] { "author_", "author.2", "files-1" },
                output);
        }

        public static IEnumerable<object[]> HasMixedDependencyGroupStylesTestData =>
            new TestDataBuilder<bool>(
                new Dictionary<string, bool>
                {
                    { Resources.Nuspecs.MixedDependencyGroupStyles, true },
                    { Resources.Nuspecs.DuplicateDependencies, true },
                },
                defaultExpected: false)
            .Build();

        [Theory]
        [MemberData(nameof(HasMixedDependencyGroupStylesTestData))]
        public void HasMixedDependencyGroupStyles(string resourceName, bool expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.HasMixedDependencyGroupStyles(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetInvalidDependencyTargetFrameworksTestData =>
            new TestDataBuilder<string[]>(
                new Dictionary<string, string[]>
                {
                    {
                        Resources.Nuspecs.InvalidDependencyTargetFrameworks,
                        new[] { "portable-net45+net-cl" }
                    },
                    {
                        Resources.Nuspecs.DuplicateDependencies,
                        new[] { "portable-net45+net-cl", "portable-net40+net-cl" }
                    },
                },
                defaultExpected: new string[0])
            .Build();

        [Theory]
        [MemberData(nameof(GetInvalidDependencyTargetFrameworksTestData))]
        public void GetInvalidDependencyTargetFrameworks(string resourceName, string[] expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetInvalidDependencyTargetFrameworks(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetWhitespaceDependencyTargetFrameworksTestData =>
            new TestDataBuilder<string[]>(
                new Dictionary<string, string[]>
                {
                    {
                        Resources.Nuspecs.WhitespaceDependencyTargetFrameworks,
                        new[] { "   ", " \n  ", " \r", "\t    " }
                    },
                    {
                        Resources.Nuspecs.DuplicateDependencies,
                        new[] { "   " }
                    },
                },
                defaultExpected: new string[0])
            .Build();

        [Theory]
        [MemberData(nameof(GetWhitespaceDependencyTargetFrameworksTestData))]
        public void GetWhitespaceDependencyTargetFrameworks(string resourceName, string[] expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetWhitespaceDependencyTargetFrameworks(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetInvalidDependencyVersionsTestData =>
            new TestDataBuilder<string[]>(
                new Dictionary<string, string[]>
                {
                    {
                        Resources.Nuspecs.InvalidDependencyVersions,
                        new[] { "[15.106.0.preview]", "1.0.0~~1" }
                    }
                },
                defaultExpected: new string[0])
            .Build();

        [Theory]
        [MemberData(nameof(GetInvalidDependencyVersionsTestData))]
        public void GetInvalidDependencyVersions(string resourceName, string[] expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetInvalidDependencyVersions(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetWhitespaceDependencyVersionsTestData =>
            new TestDataBuilder<string[]>(
                new Dictionary<string, string[]>
                {
                    {
                        Resources.Nuspecs.InvalidDependencyVersions,
                        new[] { "  ", " \r" }
                    }
                },
                defaultExpected: new string[0])
            .Build();

        [Theory]
        [MemberData(nameof(GetWhitespaceDependencyVersionsTestData))]
        public void GetWhitespaceDependencyVersions(string resourceName, string[] expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetWhitespaceDependencyVersions(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetDuplicateDependencyTargetFrameworksTestData =>
            new TestDataBuilder<ILookup<string, string>>(
                new Dictionary<string, ILookup<string, string>>
                {
                    {
                        Resources.Nuspecs.DuplicateDependencyTargetFrameworks,
                        new Dictionary<string, IEnumerable<string>>
                        {
                            {
                                ".NETFramework4.5",
                                new[] { ".NETFramework4.5", ".NETFramework4.5", ".NETFramework4.5" }
                            },
                            {
                                ".NETStandard1.1",
                                new[] { ".NETStandard1.1", ".NETStandard1.1" }
                            },
                        }.ToLookup()
                    }
                },
                defaultExpected: new Dictionary<string, IEnumerable<string>>().ToLookup())
            .Build();

        [Theory]
        [MemberData(nameof(GetDuplicateDependencyTargetFrameworksTestData))]
        public void GetDuplicateDependencyTargetFrameworks(string resourceName, ILookup<string, string> expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetDuplicateDependencyTargetFrameworks(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetDuplicateNormalizedDependencyTargetFrameworksTestData =>
            new TestDataBuilder<ILookup<NuGetFramework, string>>(
                new Dictionary<string, ILookup<NuGetFramework, string>>
                {
                    {
                        Resources.Nuspecs.DuplicateDependencyTargetFrameworks,
                        new Dictionary<NuGetFramework, IEnumerable<string>>
                        {
                            {
                                NuGetFramework.Parse(".NETFramework,Version=v4.5"),
                                new[] { ".NETFramework4.5", ".NETFramework4.5", ".NETFramework4.5", "net45", "net4.5" }
                            },
                            {
                                NuGetFramework.Parse("Unsupported,Version=v0.0"),
                                new[] { ".NETFramework4.5  ","  .netFramework4.5" }
                            },
                            {
                                NuGetFramework.Parse(".NETStandard,Version=v1.1"),
                                new[] { ".NETStandard1.1", ".NETStandard1.1" }
                            },
                        }.ToLookup()
                    },
                    {
                        Resources.Nuspecs.WhitespaceDependencyTargetFrameworks,
                        new List<KeyValuePair<NuGetFramework, IEnumerable<string>>>
                        {
                            KeyValuePair.Create(
                                (NuGetFramework) null,
                                new[] { null, "" }.AsEnumerable()),
                            KeyValuePair.Create(
                                NuGetFramework.Parse("Unsupported,Version=v0.0"),
                                new[] { "   ", " \n  ", " \r", "\t    " }.AsEnumerable()),
                        }.ToLookup()
                    },
                    {
                        Resources.Nuspecs.UnsupportedDependencyTargetFrameworks,
                        new Dictionary<NuGetFramework, IEnumerable<string>>
                        {
                            {
                                NuGetFramework.Parse("Unsupported,Version=v0.0"),
                                new[]
                                {
                                    "   .NETFramework4.5.1",
                                    ".NETFramework4.5.1  ",
                                    "   .NETFramework4.5.1  ",
                                    "Unsupported,Version=v0.0",
                                    "Unsupported,Version=v0.1",
                                    "ZAMARIN",
                                    "unsupported",
                                    "unsupported0.0",
                                    "unsupported1.0",
                                    "unsupported10",
                                }
                            }
                        }.ToLookup()
                    },
                    {
                        Resources.Nuspecs.DuplicateDependencies,
                        new List<KeyValuePair<NuGetFramework, IEnumerable<string>>>
                        {
                            KeyValuePair.Create(
                                NuGetFramework.Parse(".NETStandard,Version=v1.3"),
                                new[]
                                {
                                    ".NETStandard1.3",
                                    "netstandard1.3",
                                }.AsEnumerable()),
                            KeyValuePair.Create(
                                NuGetFramework.AnyFramework,
                                new[]
                                {
                                    null,
                                    "",
                                }.AsEnumerable()),
                            KeyValuePair.Create(
                                NuGetFramework.Parse("Unsupported,Version=v0.0"),
                                new[]
                                {
                                    "   ",
                                    "ZAMARIN",
                                }.AsEnumerable()),
                            KeyValuePair.Create(
                                (NuGetFramework) null,
                                new[]
                                {
                                    "portable-net45+net-cl",
                                    "portable-net40+net-cl",
                                }.AsEnumerable()),
                        }.ToLookup()
                    }
                },
                defaultExpected: new Dictionary<NuGetFramework, IEnumerable<string>>().ToLookup())
            .Build();

        [Theory]
        [MemberData(nameof(GetDuplicateNormalizedDependencyTargetFrameworksTestData))]
        public void GetDuplicateNormalizedDependencyTargetFrameworks(string resourceName, ILookup<NuGetFramework, string> expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetDuplicateNormalizedDependencyTargetFrameworks(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetUnsupportedDependencyTargetFrameworksTestData =>
            new TestDataBuilder<string[]>(
                new Dictionary<string, string[]>
                {
                    {
                        Resources.Nuspecs.DuplicateDependencyTargetFrameworks,
                        new[] { ".NETFramework4.5  ", "  .netFramework4.5" }
                    },
                    {
                        Resources.Nuspecs.UnsupportedDependencyTargetFrameworks,
                        new[]
                        {
                            "   .NETFramework4.5.1",
                            ".NETFramework4.5.1  ",
                            "   .NETFramework4.5.1  ",
                            "Unsupported,Version=v0.0",
                            "Unsupported,Version=v0.1",
                            "ZAMARIN",
                            "unsupported",
                            "unsupported0.0",
                            "unsupported1.0",
                            "unsupported10",
                        }
                    },
                    {
                        Resources.Nuspecs.WhitespaceDependencyTargetFrameworks,
                        new[] { "   ", " \n  ", " \r", "\t    " }
                    },
                    {
                        Resources.Nuspecs.DuplicateDependencies,
                        new[] { "   ", "ZAMARIN" }
                    },
                },
                defaultExpected: new string[0])
            .Build();

        [Theory]
        [MemberData(nameof(GetUnsupportedDependencyTargetFrameworksTestData))]
        public void GetUnsupportedDependencyTargetFrameworks(string resourceName, string[] expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetUnsupportedDependencyTargetFrameworks(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetInvalidDependencyIdsTestData =>
            new TestDataBuilder<string[]>(
                new Dictionary<string, string[]>
                {
                    {
                        Resources.Nuspecs.InvalidDependencyIds,
                        new[]
                        {
                            null,
                            null,
                            "",
                            "",
                            " ",
                            "   ",
                            "  \r ",
                            "foo bar",
                            "    foo bar baz  ",
                            "!!!",
                            "RX--Main",
                            "../jquery.TypeScript.DefinitelyTyped",
                        }
                    }
                },
                defaultExpected: new string[0])
            .Build();

        [Theory]
        [MemberData(nameof(GetInvalidDependencyIdsTestData))]
        public void GetInvalidDependencyIds(string resourceName, string[] expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetInvalidDependencyIds(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetMissingDependencyIdsTestData =>
            new TestDataBuilder<string[]>(
                new Dictionary<string, string[]>
                {
                    {
                        Resources.Nuspecs.InvalidDependencyIds,
                        new string[]
                        {
                            null,
                            null,
                        }
                    }
                },
                defaultExpected: new string[0])
            .Build();

        [Theory]
        [MemberData(nameof(GetMissingDependencyIdsTestData))]
        public void GetMissingDependencyIds(string resourceName, string[] expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetMissingDependencyIds(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetEmptyDependencyIdsTestData =>
            new TestDataBuilder<string[]>(
                new Dictionary<string, string[]>
                {
                    {
                        Resources.Nuspecs.InvalidDependencyIds,
                        new[]
                        {
                            "",
                            "",
                        }
                    }
                },
                defaultExpected: new string[0])
            .Build();

        [Theory]
        [MemberData(nameof(GetEmptyDependencyIdsTestData))]
        public void GetEmptyDependencyIds(string resourceName, string[] expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetEmptyDependencyIds(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetWhitespaceDependencyIdsTestData =>
            new TestDataBuilder<string[]>(
                new Dictionary<string, string[]>
                {
                    {
                        Resources.Nuspecs.InvalidDependencyIds,
                        new[] { " ", "   ", "  \r " }
                    }
                },
                defaultExpected: new string[0])
            .Build();

        [Theory]
        [MemberData(nameof(GetWhitespaceDependencyIdsTestData))]
        public void GetWhitespaceDependencyIds(string resourceName, string[] expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetWhitespaceDependencyIds(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetEmptyDependencyVersionsTestData =>
            new TestDataBuilder<string[]>(
                new Dictionary<string, string[]>
                {
                    {
                        Resources.Nuspecs.InvalidDependencyVersions,
                        new[]
                        {
                            "",
                            "",
                        }
                    }
                },
                defaultExpected: new string[0])
            .Build();

        [Theory]
        [MemberData(nameof(GetEmptyDependencyVersionsTestData))]
        public void GetEmptyDependencyVersions(string resourceName, string[] expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetEmptyDependencyVersions(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetMissingDependencyVersionsTestData =>
            new TestDataBuilder<string[]>(
                new Dictionary<string, string[]>
                {
                    {
                        Resources.Nuspecs.InvalidDependencyVersions,
                        new string[]
                        {
                            null,
                            null,
                        }
                    }
                },
                defaultExpected: new string[0])
            .Build();

        [Theory]
        [MemberData(nameof(GetMissingDependencyVersionsTestData))]
        public void GetMissingDependencyVersions(string resourceName, string[] expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetMissingDependencyVersions(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetFloatingDependencyVersionsTestData =>
            new TestDataBuilder<string[]>(
                new Dictionary<string, string[]>
                {
                    {
                        Resources.Nuspecs.FloatingDependencyVersions,
                        new string[]
                        {
                            "4.0.*",
                            " 4.0.1-*   ",
                        }
                    }
                },
                defaultExpected: new string[0])
            .Build();

        [Theory]
        [MemberData(nameof(GetFloatingDependencyVersionsTestData))]
        public void GetFloatingDependencyVersions(string resourceName, string[] expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetFloatingDependencyVersions(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> GetDuplicateDependenciesTestData =>
            new TestDataBuilder<ILookup<NuGetFramework, string>>(
                new Dictionary<string, ILookup<NuGetFramework, string>>
                {
                    {
                        Resources.Nuspecs.InvalidDependencyIds,
                        new Dictionary<NuGetFramework, IEnumerable<string>>
                        {
                            {
                                NuGetFramework.AnyFramework,
                                new[] { null, "" }.AsEnumerable()
                            },
                        }.ToLookup()
                    },
                    {
                        Resources.Nuspecs.DuplicateDependencies,
                        new List<KeyValuePair<NuGetFramework, IEnumerable<string>>>
                        {
                            KeyValuePair.Create(
                                NuGetFramework.Parse(".NETStandard,Version=v1.1"),
                                new[]
                                {
                                    "NETStandard.Library",
                                    "Microsoft.CSharp",
                                    "System.ComponentModel.TypeConverter",
                                }.AsEnumerable()),
                            KeyValuePair.Create(
                                NuGetFramework.Parse(".NETStandard,Version=v1.3"),
                                new[]
                                {
                                    "NETStandard.Library",
                                    "Microsoft.CSharp",
                                }.AsEnumerable()),
                            KeyValuePair.Create(
                                NuGetFramework.AnyFramework,
                                new[]
                                {
                                    "NETStandard.Library",
                                }.AsEnumerable()),
                            KeyValuePair.Create(
                                NuGetFramework.UnsupportedFramework,
                                new[]
                                {
                                    "NETStandard.Library",
                                }.AsEnumerable()),
                            KeyValuePair.Create(
                                (NuGetFramework) null,
                                new[]
                                {
                                    "NETStandard.Library",
                                }.AsEnumerable()),
                        }.ToLookup()
                    }
                },
                defaultExpected: new Dictionary<NuGetFramework, IEnumerable<string>>().ToLookup())
            .Build();

        [Theory]
        [MemberData(nameof(GetDuplicateDependenciesTestData))]
        public void GetDuplicateDependencies(string resourceName, ILookup<NuGetFramework, string> expected)
        {
            // Arrange
            var nuspec = Resources.LoadXml(resourceName);

            // Act
            var actual = NuspecUtility.GetDuplicateDependencies(nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        private class TestDataBuilder<T>
        {
            private static IReadOnlyList<string> ResourceNames => typeof(Resources.Nuspecs)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(x => x.IsLiteral && !x.IsInitOnly)
                .Select(x => x.GetValue(null))
                .Cast<string>()
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            private readonly IReadOnlyDictionary<string, T> _expected;
            private readonly T _defaultExpected;

            public TestDataBuilder(IReadOnlyDictionary<string, T> resourceNameToExpected, T defaultExpected)
            {
                _expected = resourceNameToExpected;
                _defaultExpected = defaultExpected;
            }

            public IEnumerable<object[]> Build()
            {
                return ResourceNames
                    .Select(x => new object[] { x, GetExpected(x) })
                    .ToList();
            }

            public T GetExpected(string resourceName)
            {
                if (_expected.TryGetValue(resourceName, out var expected))
                {
                    return expected;
                }

                return _defaultExpected;
            }
        }
    }
}

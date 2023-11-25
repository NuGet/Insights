// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.BuildVersionSet
{
    public class VersionSetTest
    {
        [Fact]
        public void TheGetUncheckedIdsMethod_ReturnsAllIds()
        {
            Assert.Equal(
                new[] { "DeletedA", "Knapcode.TorSharp", "Newtonsoft.Json", "NoVersions" },
                Target.GetUncheckedIds().OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void TheGetUncheckedIdsMethod_IsAffectedByDidIdEverExist()
        {
            Target.TryGetId("knapcode.torsharp", out _);

            Assert.Equal(
                new[] { "DeletedA", "Newtonsoft.Json", "NoVersions" },
                Target.GetUncheckedIds().OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void TheGetUncheckedIdsMethod_IsNotAffectedByDidVersionEverExist()
        {
            Target.TryGetVersion("knapcode.torsharp", "1.0.0-beta", out _);

            Assert.Equal(
                new[] { "DeletedA", "Knapcode.TorSharp", "Newtonsoft.Json", "NoVersions" },
                Target.GetUncheckedIds().OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void TheGetUncheckedVersionsMethod_ReturnsAllVersions()
        {
            Assert.Equal(
                new[] { "10.0.1-beta", "11.0.1", "9.0.1" },
                Target.GetUncheckedVersions("newtonsoft.json").OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void TheGetUncheckedVersionsMethod_IsAffectedByDidVersionEverExist()
        {
            Target.TryGetVersion("newtonsoft.json", "10.0.1-BETA", out _);

            Assert.Equal(
                new[] { "11.0.1", "9.0.1" },
                Target.GetUncheckedVersions("newtonsoft.json").OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void TheGetUncheckedVersionsMethod_IsNotAffectedByDidIdEverExist()
        {
            Target.TryGetId("newtonsoft.json", out _);

            Assert.Equal(
                new[] { "10.0.1-beta", "11.0.1", "9.0.1" },
                Target.GetUncheckedVersions("newtonsoft.json").OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        [Theory]
        [InlineData("Knapcode.TorSharp", "1.0.0-beta", true)] // Exact casing
        [InlineData("Knapcode.TorSharp", "1.0.0.0-beta", false)] // Non-normalized input
        [InlineData("newtonsoft.json", "9.0.1", true)] // Case insensitive on ID
        [InlineData("Newtonsoft.Json", "10.0.1-BETA", true)] // Case insensitive on version
        [InlineData("DeletedA", "2.0.0", true)] // Deleted version
        [InlineData("DeletedA", "1.0.0", false)] // Non-existent version
        [InlineData("NoVersions", "1.0.0", false)] // Non-existent version, no versions
        [InlineData("NeverExisted", "1.0.0", false)] // Non-existent ID
        public void TheDidVersionEverExistMethod(string id, string version, bool expected)
        {
            Assert.Equal(expected, Target.TryGetVersion(id, version, out _));
        }

        [Theory]
        [InlineData("Knapcode.TorSharp", true)] // Exact casing
        [InlineData("newtonsoft.json", true)] // Case insensitive
        [InlineData("DeletedA", true)] // All deleted versions
        [InlineData("NoVersions", true)] // No versions
        [InlineData("DeletedB", false)] // Non-existent
        public void TheDidIdEverExistMethod(string id, bool expected)
        {
            Assert.Equal(expected, Target.TryGetId(id, out _));
        }

        public VersionSetTest()
        {
            Target = new VersionSet(
                new DateTimeOffset(2021, 5, 1, 12, 30, 0, 0, TimeSpan.Zero),
                ToReadableKey(new CaseInsensitiveDictionary<CaseInsensitiveDictionary<ReadableKey<bool>>>
                {
                    {
                        "Newtonsoft.Json",
                        ToReadableKey(new CaseInsensitiveDictionary<bool>
                        {
                            { "9.0.1", false },
                            { "10.0.1-beta", true },
                            { "11.0.1", false },
                        })
                    },
                    {
                        "Knapcode.TorSharp",
                        ToReadableKey(new CaseInsensitiveDictionary<bool>
                        {
                            { "1.0.0-beta", false },
                        })
                    },
                    {
                        "DeletedA",
                        ToReadableKey(new CaseInsensitiveDictionary<bool>
                        {
                            { "2.0.0", true },
                        })
                    },
                    {
                        "NoVersions",
                        ToReadableKey(new CaseInsensitiveDictionary<bool>())
                    },
                }));
        }

        private CaseInsensitiveDictionary<ReadableKey<T>> ToReadableKey<T>(CaseInsensitiveDictionary<T> input)
        {
            var output = new CaseInsensitiveDictionary<ReadableKey<T>>();
            foreach ((var key, var value) in input)
            {
                output.Add(key, ReadableKey.Create(key, value));
            }
            return output;
        }

        public VersionSet Target { get; }
    }
}

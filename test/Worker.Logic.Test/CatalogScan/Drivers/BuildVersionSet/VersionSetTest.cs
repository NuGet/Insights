using System;
using System.Linq;
using Xunit;

namespace Knapcode.ExplorePackages.Worker.BuildVersionSet
{
    public class VersionSetTest
    {
        [Fact]
        public void TheGetUncheckedIdsMethod_ReturnsAllIds()
        {
            Assert.Equal(
                new[] { "DeletedA", "Knapcode.TorSharp", "Newtonsoft.Json", "NoVersions" },
                Target.GetUncheckedIds().OrderBy(x => x).ToArray());
        }

        [Fact]
        public void TheGetUncheckedIdsMethod_IsAffectedByDidIdEverExist()
        {
            Target.DidIdEverExist("knapcode.torsharp");

            Assert.Equal(
                new[] { "DeletedA", "Newtonsoft.Json", "NoVersions" },
                Target.GetUncheckedIds().OrderBy(x => x).ToArray());
        }

        [Fact]
        public void TheGetUncheckedIdsMethod_IsAffectedByDidVersionEverExist_WhenVersionExists()
        {
            Target.DidVersionEverExist("knapcode.torsharp", "1.0.0-beta");

            Assert.Equal(
                new[] { "DeletedA", "Newtonsoft.Json", "NoVersions" },
                Target.GetUncheckedIds().OrderBy(x => x).ToArray());
        }

        [Fact]
        public void TheGetUncheckedIdsMethod_IsAffectedByDidVersionEverExist_WhenVersionDoesNotExist()
        {
            Target.DidVersionEverExist("knapcode.torsharp", "9.9.9");

            Assert.Equal(
                new[] { "DeletedA", "Newtonsoft.Json", "NoVersions" },
                Target.GetUncheckedIds().OrderBy(x => x).ToArray());
        }

        [Fact]
        public void TheGetUncheckedVersionsMethod_ReturnsAllVersions()
        {
            Assert.Equal(
                new[] { "10.0.1-beta", "11.0.1", "9.0.1" },
                Target.GetUncheckedVersions("newtonsoft.json").OrderBy(x => x).ToArray());
        }

        [Fact]
        public void TheGetUncheckedVersionsMethod_IsAffectedByDidVersionEverExist()
        {
            Target.DidVersionEverExist("newtonsoft.json", "10.0.1-BETA");

            Assert.Equal(
                new[] { "11.0.1", "9.0.1" },
                Target.GetUncheckedVersions("newtonsoft.json").OrderBy(x => x).ToArray());
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
            Assert.Equal(expected, Target.DidVersionEverExist(id, version));
        }

        [Theory]
        [InlineData("Knapcode.TorSharp", true)] // Exact casing
        [InlineData("newtonsoft.json", true)] // Case insensitive
        [InlineData("DeletedA", true)] // All deleted versions
        [InlineData("NoVersions", true)] // No versions
        [InlineData("DeletedB", false)] // Non-existent
        public void TheDidIdEverExistMethod(string id, bool expected)
        {
            Assert.Equal(expected, Target.DidIdEverExist(id));
        }

        public VersionSetTest()
        {
            Target = new VersionSet(
                new DateTimeOffset(2021, 5, 1, 12, 30, 0, 0, TimeSpan.Zero),
                new CaseInsensitiveDictionary<CaseInsensitiveDictionary<bool>>
                {
                    {
                        "Newtonsoft.Json",
                        new CaseInsensitiveDictionary<bool>
                        {
                            { "9.0.1", false },
                            { "10.0.1-beta", true },
                            { "11.0.1", false },
                        }
                    },
                    {
                        "Knapcode.TorSharp",
                        new CaseInsensitiveDictionary<bool>
                        {
                            { "1.0.0-beta", false },
                        }
                    },
                    {
                        "DeletedA",
                        new CaseInsensitiveDictionary<bool>
                        {
                            { "2.0.0", true },
                        }
                    },
                    {
                        "NoVersions",
                        new CaseInsensitiveDictionary<bool>()
                    },
                });
        }

        public VersionSet Target { get; }
    }
}

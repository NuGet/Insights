using Xunit;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageBlobNameProviderTest
    {
        [Theory]
        [InlineData("A", "1.0.0", "a/1.0.0/latest.nuspec")]
        [InlineData("a", "1.0.0", "a/1.0.0/latest.nuspec")]
        [InlineData("A", "2.0.0", "a/2.0.0/latest.nuspec")]
        [InlineData("A", "2.0.0-BETA", "a/2.0.0-beta/latest.nuspec")]
        [InlineData("A", "2.0.0-beta", "a/2.0.0-beta/latest.nuspec")]
        [InlineData("AB", "1.0.0", "ab/1.0.0/latest.nuspec")]
        [InlineData("ABC", "1.0.0", "abc/1.0.0/latest.nuspec")]
        [InlineData("ABCD", "1.0.0", "abcd/1.0.0/latest.nuspec")]
        [InlineData("ABCDE", "1.0.0", "abcde/1.0.0/latest.nuspec")]
        [InlineData("A.B", "1.0.0", "a.b/1.0.0/latest.nuspec")]
        [InlineData("_", "1.0.0", "_/1.0.0/latest.nuspec")]
        [InlineData("_._", "1.0.0", "_._/1.0.0/latest.nuspec")]
        [InlineData("新包", "1.0.0", "新包/1.0.0/latest.nuspec")]
        [InlineData("ဪ", "1.0.0", "ဪ/1.0.0/latest.nuspec")]
        [InlineData("настройкирегистрации", "1.0.0", "настройкирегистрации/1.0.0/latest.nuspec")]
        [InlineData("Iıİi", "1.0.0", "iıİi/1.0.0/latest.nuspec")]
        public void GetLatestNuspecBlobName(string id, string version, string expected)
        {
            // Arrange
            var target = new PackageBlobNameProvider();

            // Act
            var actual = target.GetLatestBlobName(id, version, FileArtifactType.Nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}

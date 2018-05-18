using Xunit;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageFilePathProviderTest
    {
        private const string Version = "1.0.0";
        private readonly ExplorePackagesSettings _settings;

        public PackageFilePathProviderTest()
        {
            _settings = new ExplorePackagesSettings
            {
                PackagePath = @"C:\data",
            };
        }

        [Theory]
        [InlineData("A", @"C:\data\a\_\_\_\a\1.0.0\a.nuspec")]
        [InlineData("AB", @"C:\data\a\b\_\_\ab\1.0.0\ab.nuspec")]
        [InlineData("ABC", @"C:\data\a\b\c\_\abc\1.0.0\abc.nuspec")]
        [InlineData("ABCD", @"C:\data\a\b\c\d\abcd\1.0.0\abcd.nuspec")]
        [InlineData("ABCDE", @"C:\data\a\b\c\d\abcde\1.0.0\abcde.nuspec")]
        [InlineData("A.B", @"C:\data\a\_\b\_\a.b\1.0.0\a.b.nuspec")]
        [InlineData("_", @"C:\data\_\_\_\_\_\1.0.0\_.nuspec")]
        [InlineData("_._", @"C:\data\_\_\_\_\_._\1.0.0\_._.nuspec")]
        [InlineData("新包", @"C:\data\新\包\_\_\新包\1.0.0\新包.nuspec")]
        [InlineData("ဪ", @"C:\data\ဪ\_\_\_\ဪ\1.0.0\ဪ.nuspec")]
        [InlineData("настройкирегистрации", @"C:\data\н\а\с\т\настройкирегистрации\1.0.0\настройкирегистрации.nuspec")]
        [InlineData("Iıİi", @"C:\data\i\ı\İ\i\iıİi\1.0.0\iıİi.nuspec")]
        public void GetLatestNuspecFilePathWithFourIdLetters(string id, string expected)
        {
            // Arrange
            var target = new PackageFilePathProvider(_settings, PackageFilePathStyle.FourIdLetters);

            // Act
            var actual = target.GetLatestFilePath(id, Version, FileArtifactType.Nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("A", "1.0.0", @"C:\data\87\a7\a\1.0.0\latest.nuspec")]
        [InlineData("a", "1.0.0", @"C:\data\87\a7\a\1.0.0\latest.nuspec")]
        [InlineData("A", "2.0.0", @"C:\data\9f\8a\a\2.0.0\latest.nuspec")]
        [InlineData("A", "2.0.0-BETA", @"C:\data\77\47\a\2.0.0-beta\latest.nuspec")]
        [InlineData("A", "2.0.0-beta", @"C:\data\77\47\a\2.0.0-beta\latest.nuspec")]
        [InlineData("AB", "1.0.0", @"C:\data\53\ca\ab\1.0.0\latest.nuspec")]
        [InlineData("ABC", "1.0.0", @"C:\data\0a\cd\abc\1.0.0\latest.nuspec")]
        [InlineData("ABCD", "1.0.0", @"C:\data\a5\c5\abcd\1.0.0\latest.nuspec")]
        [InlineData("ABCDE", "1.0.0", @"C:\data\51\29\abcde\1.0.0\latest.nuspec")]
        [InlineData("A.B", "1.0.0", @"C:\data\b8\fb\a.b\1.0.0\latest.nuspec")]
        [InlineData("_", "1.0.0", @"C:\data\26\b0\_\1.0.0\latest.nuspec")]
        [InlineData("_._", "1.0.0", @"C:\data\ca\8a\_._\1.0.0\latest.nuspec")]
        [InlineData("新包", "1.0.0", @"C:\data\cf\f1\新包\1.0.0\latest.nuspec")]
        [InlineData("ဪ", "1.0.0", @"C:\data\37\6f\ဪ\1.0.0\latest.nuspec")]
        [InlineData("настройкирегистрации", "1.0.0", @"C:\data\a0\4c\настройкирегистрации\1.0.0\latest.nuspec")]
        [InlineData("Iıİi", "1.0.0", @"C:\data\0c\f5\iıİi\1.0.0\latest.nuspec")]
        public void GetLatestNuspecFilePathWithTwoByteIdentityHash(string id, string version, string expected)
        {
            // Arrange
            var target = new PackageFilePathProvider(_settings, PackageFilePathStyle.TwoByteIdentityHash);

            // Act
            var actual = target.GetLatestFilePath(id, version, FileArtifactType.Nuspec);

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}

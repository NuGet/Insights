using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Knapcode.ExplorePackages
{
    public class CsvUtlityTest
    {
        private static readonly string BaseDir = "csv-spectrum";
        private static readonly string CsvsDir = Path.Combine(BaseDir, "csvs");
        private static readonly string JsonDir = Path.Combine(BaseDir, "json");

        [Theory]
        [MemberData(nameof(TestData))]
        public void ReadsAllData(string testCase)
        {
            // Arrange
            var csv = Resources.LoadStringReader(Path.Combine(CsvsDir, $"{testCase}.csv"));
            var expected = Resources.LoadJson<List<List<string>>>(Path.Combine(JsonDir, $"{testCase}.json"));
            var lines = new List<List<string>>();
            var fields = new List<string>();
            var builder = new StringBuilder();

            // Act
            while (CsvUtility.TryReadLine(csv, fields, builder))
            {
                lines.Add(fields.ToList());
            }

            // Assert
            Assert.Equal(expected, lines);
        }

        public static IEnumerable<object[]> TestData => Directory
            .EnumerateFiles(Path.Combine("TestData", CsvsDir), "*.csv")
            .Select(x => Path.GetFileNameWithoutExtension(x))
            .Select(x => new object[] { x });
    }
}

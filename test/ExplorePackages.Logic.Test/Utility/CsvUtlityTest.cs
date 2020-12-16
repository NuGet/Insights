using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Knapcode.ExplorePackages
{
    public class CsvUtlityTest
    {
        private static readonly string BaseDir = "csv-spectrum";
        private static readonly string CsvsDir = Path.Combine(BaseDir, "csvs");
        private static readonly string JsonDir = Path.Combine(BaseDir, "json");

        public static IEnumerable<object[]> TestData => Directory
            .EnumerateFiles(Path.Combine("TestData", CsvsDir), "*.csv")
            .Select(x => Path.GetFileNameWithoutExtension(x))
            .Select(x => new object[] { x });
    }
}

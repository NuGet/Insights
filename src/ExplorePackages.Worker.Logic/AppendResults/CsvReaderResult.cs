using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Worker
{
    public class CsvReaderResult<T> where T : ICsvRecord
    {
        public CsvReaderResult(CsvReaderResultType type, List<T> records)
        {
            Type = type;
            Records = records;
        }

        public CsvReaderResultType Type { get; }
        public List<T> Records { get; }
    }
}

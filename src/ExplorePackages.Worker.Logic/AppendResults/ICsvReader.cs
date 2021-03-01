using System.IO;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICsvReader
    {
        CsvReaderResult<T> GetRecords<T>(TextReader reader, int bufferSize) where T : ICsvRecord<T>, new();
    }
}

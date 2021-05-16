using System.IO;

namespace NuGet.Insights.Worker
{
    public interface ICsvReader
    {
        CsvReaderResult<T> GetRecords<T>(TextReader reader, int bufferSize) where T : ICsvRecord;
    }
}

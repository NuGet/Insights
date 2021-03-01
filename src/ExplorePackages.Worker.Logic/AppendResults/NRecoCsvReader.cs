using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    public class NRecoCsvReader : ICsvReader
    {
        /// <summary>
        /// Needed due to these big guys:
        /// - PageRender.Razor 1.6.0 - massive number of content files, ~2,078,825 bytes
        /// - PageRender.Razor 1.7.0 - massive number of content files, ~2,095,119 bytes
        /// </summary>
        public const int MaxBufferSize = 4 * 1024 * 1024;

        private readonly ILogger<NRecoCsvReader> _logger;

        public NRecoCsvReader(ILogger<NRecoCsvReader> logger)
        {
            _logger = logger;
        }

        public CsvReaderResult<T> GetRecords<T>(TextReader reader, int bufferSize) where T : ICsvRecord<T>, new()
        {
            var allRecords = new List<T>();
            var csvReader = new NReco.Csv.CsvReader(reader)
            {
                BufferSize = bufferSize,
            };

            var factory = new T();

            try
            {
                while (csvReader.Read())
                {
                    var i = 0;
                    var record = factory.Read(() => csvReader[i++]);
                    allRecords.Add(record);
                }
            }
            catch (InvalidDataException ex) when (
                ex.Message.StartsWith("CSV line #")
                && ex.Message.Contains($" length exceedes buffer size ({bufferSize})")
                && bufferSize < MaxBufferSize)
            {
                _logger.LogWarning(ex, "Could not read CSV with buffer size {BufferSize}.", bufferSize);
                return new CsvReaderResult<T>(CsvReaderResultType.BufferTooSmall, records: null);
            }

            return new CsvReaderResult<T>(CsvReaderResultType.Success, allRecords);
        }
    }
}

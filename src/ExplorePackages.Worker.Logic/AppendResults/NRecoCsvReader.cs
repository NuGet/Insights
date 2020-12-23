using System.Collections.Generic;
using System.IO;

namespace Knapcode.ExplorePackages.Worker
{
    public class NRecoCsvReader : ICsvReader
    {
        public List<T> GetRecords<T>(TextReader reader) where T : ICsvRecord, new()
        {
            var allRecords = new List<T>();
            var csvReader = new NReco.Csv.CsvReader(reader);
            while (csvReader.Read())
            {
                var record = new T();
                var i = 0;
                record.Read(() => csvReader[i++]);
                allRecords.Add(record);
            }

            return allRecords;
        }
    }
}

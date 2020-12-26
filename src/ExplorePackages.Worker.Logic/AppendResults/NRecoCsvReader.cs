using System.Collections.Generic;
using System.IO;

namespace Knapcode.ExplorePackages.Worker
{
    public class NRecoCsvReader : ICsvReader
    {
        public List<T> GetRecords<T>(TextReader reader) where T : ICsvRecord<T>, new()
        {
            var allRecords = new List<T>();
            var csvReader = new NReco.Csv.CsvReader(reader);
            var factory = new T();
            while (csvReader.Read())
            {
                var i = 0;
                var record = factory.Read(() => csvReader[i++]);
                allRecords.Add(record);
            }

            return allRecords;
        }
    }
}

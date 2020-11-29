using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICsvReader
    {
        List<T> GetRecords<T>(MemoryStream stream) where T : ICsvWritable, new();
    }

    public class TinyCsvReader : ICsvReader
    {
        public List<T> GetRecords<T>(MemoryStream stream) where T : ICsvWritable, new()
        {
            var allRecords = new List<T>();

            using (var reader = new StreamReader(stream))
            {
                var options = new TinyCsvParser.Tokenizer.RFC4180.Options('"', '"', ',');
                var tokenizer = new TinyCsvParser.Tokenizer.RFC4180.RFC4180Tokenizer(options);

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var record = new T();
                    var fields = tokenizer.Tokenize(line);
                    record.Read(i => fields[i]);
                    allRecords.Add(record);
                }
            }

            return allRecords;
        }
    }

    public class LumenWorksCsvReader : ICsvReader
    {
        public List<T> GetRecords<T>(MemoryStream stream) where T : ICsvWritable, new()
        {
            var allRecords = new List<T>();

            using (var reader = new StreamReader(stream))
            using (var csvReader = new LumenWorks.Framework.IO.Csv.CsvReader(reader, hasHeaders: false))
            {
                while (csvReader.ReadNextRecord())
                {
                    var record = new T();
                    record.Read(i => csvReader[i]);
                    allRecords.Add(record);
                }
            }

            return allRecords;
        }
    }

    public class FastCsvParserCsvReader : ICsvReader
    {
        public List<T> GetRecords<T>(MemoryStream stream) where T : ICsvWritable, new()
        {
            var allRecords = new List<T>();

            using (var parser = new CsvParser.CsvReader(stream, Encoding.UTF8))
            {
                while (parser.MoveNext())
                {
                    var record = new T();
                    record.Read(i => parser.Current[i]);
                    allRecords.Add(record);
                }
            }

            return allRecords;
        }
    }

    public class ServiceStackTextCsvReader : ICsvReader
    {
        public List<T> GetRecords<T>(MemoryStream stream) where T : ICsvWritable, new()
        {
            var allRecords = new List<T>();

            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var record = new T();
                    var fields = ServiceStack.Text.CsvReader.ParseFields(line);
                    record.Read(i => fields[i]);
                    allRecords.Add(record);
                }
            }

            return allRecords;
        }
    }

    public class CsvHelperCsvReader : ICsvReader
    {
        public List<T> GetRecords<T>(MemoryStream stream) where T : ICsvWritable, new()
        {
            var allRecords = new List<T>();

            using (var reader = new StreamReader(stream))
            {
                var csvReader = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture);
                while (csvReader.Read())
                {
                    var record = new T();
                    record.Read(i => csvReader[i]);
                    allRecords.Add(record);
                }
            }

            return allRecords;
        }
    }

    public class NRecoCsvReader : ICsvReader
    {
        public List<T> GetRecords<T>(MemoryStream stream) where T : ICsvWritable, new()
        {
            var allRecords = new List<T>();

            using (var reader = new StreamReader(stream))
            {
                var csvReader = new NReco.Csv.CsvReader(reader);
                while (csvReader.Read())
                {
                    var record = new T();
                    record.Read(i => csvReader[i]);
                    allRecords.Add(record);
                }
            }

            return allRecords;
        }
    }

    public class CustomCsvReader : ICsvReader
    {
        public List<T> GetRecords<T>(MemoryStream stream) where T : ICsvWritable, new()
        {
            var allRecords = new List<T>();
            var fields = new List<string>();
            var builder = new StringBuilder();

            using (var reader = new StreamReader(stream))
            {
                while (CsvUtility.TryReadLine(reader, fields, builder))
                {
                    var record = new T();
                    record.Read(i => fields[i]);
                    allRecords.Add(record);
                }
            }

            return allRecords;
        }
    }
}

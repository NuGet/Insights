using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Knapcode.ExplorePackages
{
    public interface ICsvWritable
    {
        public void Write(TextWriter writer);
        public void Write(NReco.Csv.CsvWriter writer);
        public bool TryRead(TextReader reader, List<string> fields, StringBuilder builder);
        public bool TryRead(NReco.Csv.CsvReader reader);
    }
}

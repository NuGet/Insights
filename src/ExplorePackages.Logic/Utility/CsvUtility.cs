using System.IO;

namespace Knapcode.ExplorePackages
{
    public class CsvUtility
    {
        public static void WriteWithQuotes(TextWriter writer, string value)
        {
            if (value == null)
            {
                return;
            }

            if (value.StartsWith(' ')
                || value.EndsWith(' ')
                || value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) > -1)
            {
                writer.Write('"');
                writer.Write(value.Replace("\"", "\"\""));
                writer.Write('"');
            }
            else
            {
                writer.Write(value);
            }
        }
    }
}

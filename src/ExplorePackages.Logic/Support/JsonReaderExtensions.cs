using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public static class JsonReaderExtensions
    {
        public static async Task ReadAsStartArrayAsync(this JsonReader reader)
        {
            if (!await reader.ReadAsync() || reader.TokenType != JsonToken.StartArray)
            {
                throw new JsonReaderException("Error reading start array.");
            }
        }

        public static async Task ReadAsEndArrayAsync(this JsonReader reader)
        {
            if (!await reader.ReadAsync() || reader.TokenType != JsonToken.EndArray)
            {
                throw new JsonReaderException("Error reading end array.");
            }
        }

        public static async Task<long> ReadAsInt64Async(this JsonReader reader)
        {
            if (!await reader.ReadAsync() || reader.TokenType != JsonToken.Integer)
            {
                throw new JsonReaderException("Error reading integer.");
            }

            return (long)reader.Value;
        }

        public static async Task ReadRequiredAsync(this JsonReader reader)
        {
            if (!await reader.ReadAsync())
            {
                throw new JsonReaderException("Unexpected end of JSON.");
            }
        }
    }
}

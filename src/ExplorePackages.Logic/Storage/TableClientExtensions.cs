using System;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

namespace Knapcode.ExplorePackages
{
    public static class TableClientExtensions
    {
        public static async Task<T> GetEntityOrNullAsync<T>(this TableClient table, string partitionKey, string rowKey) where T : class, ITableEntity, new()
        {
            try
            {
                return await table.GetEntityAsync<T>(partitionKey, rowKey);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        /// <summary>
        /// See: https://github.com/Azure/azure-sdk-for-net/issues/19723
        /// </summary>
        public static void UpdateETagAndTimestamp(this ITableEntity entity, Response response)
        {
            entity.ETag = response.Headers.ETag.Value;
            entity.Timestamp = GetTimestampFromETag(entity.ETag);
        }

        private static DateTimeOffset GetTimestampFromETag(ETag etag)
        {
            var etagStr = etag.ToString();
            const string etagPrefix = "W/\"datetime'";
            if (!etagStr.StartsWith(etagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"The etag from Table Storage does not have the expected prefix: {etagPrefix}");
            }

            const string etagSuffix = "'\"";
            if (!etagStr.EndsWith(etagSuffix, StringComparison.Ordinal))
            {
                throw new ArgumentException($"The etag from Table Storage does not have the expected suffix: {etagSuffix}");
            }

            var encodedTimestamp = etagStr.Substring(etagPrefix.Length, etagStr.Length - (etagPrefix.Length + etagSuffix.Length));
            var unencodedTimestamp = Uri.UnescapeDataString(encodedTimestamp);
            var parsedTimestamp = DateTimeOffset.Parse(unencodedTimestamp, CultureInfo.InvariantCulture);

            if (parsedTimestamp.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException("The timestamp in the Table Storage etag is expected to be UTC.");
            }

            return parsedTimestamp;
        }
    }
}

using System;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;

namespace Knapcode.ExplorePackages
{
    public static class TableExtensions
    {
        public static AsyncPageable<TableItem> GetTablesAsync(this TableServiceClient client, string prefix)
        {
            var prefixQuery = TableClient.CreateQueryFilter<TableItem>(
                x => x.TableName.CompareTo(prefix) >= 0
                  && x.TableName.CompareTo(prefix + char.MaxValue) <= 0);

            return client.GetTablesAsync(filter: prefixQuery);
        }

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

        public static async Task<bool> ExistsAsync(this TableClient table)
        {
            try
            {
                await table
                    .QueryAsync<TableEntity>(x => true, maxPerPage: 1, select: new[] { StorageUtility.PartitionKey })
                    .FirstAsync();
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public static async Task DeleteEntityAsync<T>(this TableClient table, T entity, ETag ifMatch) where T : class, ITableEntity, new()
        {
            await table.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ifMatch);
        }

        public static async Task<int> GetEntityCountLowerBoundAsync(this TableClient table, string partitionKey, QueryLoopMetrics metrics)
        {
            return await GetEntityCountLowerBoundAsync<TableEntity>(
                table,
                x => x.PartitionKey == partitionKey,
                metrics);
        }

        public static async Task<int> GetEntityCountLowerBoundAsync(this TableClient table, string minPartitionKey, string maxPartitionKey, QueryLoopMetrics metrics)
        {
            return await GetEntityCountLowerBoundAsync<TableEntity>(
                table,
                x => x.PartitionKey.CompareTo(minPartitionKey) >= 0 && x.PartitionKey.CompareTo(maxPartitionKey) <= 0,
                metrics);
        }

        private static async Task<int> GetEntityCountLowerBoundAsync<T>(TableClient table, Expression<Func<T, bool>> filter, QueryLoopMetrics metrics)
            where T : class, ITableEntity, new()
        {
            using (metrics)
            {
                await using var enumerator = table
                    .QueryAsync<T>(
                        filter,
                        maxPerPage: 1000,
                        select: new[] { StorageUtility.RowKey })
                    .AsPages()
                    .GetAsyncEnumerator();

                if (await enumerator.MoveNextAsync(metrics))
                {
                    return enumerator.Current.Values.Count;
                }

                return 0;
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

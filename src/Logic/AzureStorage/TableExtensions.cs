// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using NuGet.Insights.StorageNoOpRetry;

#nullable enable

namespace NuGet.Insights
{
    public static class TableExtensions
    {
        public static AsyncPageable<TableItem> QueryAsync(this TableServiceClientWithRetryContext client, string prefix)
        {
            return client.QueryAsync(
                x => string.Compare(x.Name, prefix, StringComparison.Ordinal) >= 0
                  && string.Compare(x.Name, prefix + char.MaxValue, StringComparison.Ordinal) <= 0);
        }

        public static async Task<T?> GetEntityOrNullAsync<T>(
            this TableClientWithRetryContext table,
            string partitionKey,
            string rowKey,
            IEnumerable<string>? select = null) where T : class, ITableEntity, new()
        {
            try
            {
                return await table.GetEntityAsync<T>(partitionKey, rowKey, select);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public static async Task<bool> ExistsAsync(this TableClientWithRetryContext table)
        {
            try
            {
                await table.GetEntityAsync<TableEntity>("does-table-exist", "", select: []);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return ex.ErrorCode != TableErrorCode.TableNotFound.ToString();
            }
        }

        public static async Task DeleteEntityAsync<T>(this TableClientWithRetryContext table, T entity, ETag ifMatch) where T : class, ITableEntity, new()
        {
            await table.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ifMatch);
        }

        public static async Task<int> GetEntityCountLowerBoundAsync(this TableClientWithRetryContext table, QueryLoopMetrics metrics)
        {
            return await GetEntityCountLowerBoundAsync<TableEntity>(
                table,
                filter: x => true,
                metrics);
        }

        public static async Task<int> GetEntityCountLowerBoundAsync(this TableClientWithRetryContext table, string partitionKey, QueryLoopMetrics metrics)
        {
            return await GetEntityCountLowerBoundAsync<TableEntity>(
                table,
                x => x.PartitionKey == partitionKey,
                metrics);
        }

        public static async Task<int> GetEntityCountLowerBoundAsync(this TableClientWithRetryContext table, string minPartitionKey, string maxPartitionKey, QueryLoopMetrics metrics)
        {
            return await GetEntityCountLowerBoundAsync<TableEntity>(
                table,
                x => string.Compare(x.PartitionKey, minPartitionKey, StringComparison.Ordinal) >= 0
                  && string.Compare(x.PartitionKey, maxPartitionKey, StringComparison.Ordinal) <= 0,
                metrics);
        }

        private static async Task<int> GetEntityCountLowerBoundAsync<T>(TableClientWithRetryContext table, Expression<Func<T, bool>> filter, QueryLoopMetrics metrics)
            where T : class, ITableEntity, new()
        {
            using (metrics)
            {
                await using var enumerator = table
                    .QueryAsync(
                        filter,
                        maxPerPage: 1000,
                        select: [StorageUtility.RowKey])
                    .AsPages()
                    .GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(metrics))
                {
                    if (enumerator.Current.Values.Count > 0)
                    {
                        return enumerator.Current.Values.Count;
                    }
                }

                return 0;
            }
        }

        /// <summary>
        /// See: https://github.com/Azure/azure-sdk-for-net/issues/19723
        /// </summary>
        public static void UpdateETag(this ITableEntity entity, Response response)
        {
            entity.ETag = response.Headers.ETag!.Value;
        }
    }
}

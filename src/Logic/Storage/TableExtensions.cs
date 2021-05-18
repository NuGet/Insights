// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;

namespace NuGet.Insights
{
    public static class TableExtensions
    {
        public static AsyncPageable<TableItem> QueryAsync(this TableServiceClient client, string prefix)
        {
            return client.QueryAsync(
                x => x.Name.CompareTo(prefix) >= 0
                && x.Name.CompareTo(prefix + char.MaxValue) <= 0);
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
                    .QueryAsync(
                        filter,
                        maxPerPage: 1000,
                        select: new[] { StorageUtility.RowKey })
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
            entity.ETag = response.Headers.ETag.Value;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryTableServiceClient
    {
        private IEnumerable<Page<TableItem>> GetTablePages(string? filter, int? maxPerPage)
        {
            if (filter is not null)
            {
                throw new NotSupportedException();
            }

            return GetTablePages(x => true, maxPerPage);
        }

        private IEnumerable<Page<TableItem>> GetTablePages(Expression<Func<TableItem, bool>> filter, int? maxPerPage)
        {
            if (maxPerPage.HasValue && (maxPerPage.Value < 1 || maxPerPage.Value > StorageUtility.MaxTakeCount))
            {
                throw new ArgumentOutOfRangeException(nameof(maxPerPage));
            }

            var maxPerPageValue = maxPerPage.GetValueOrDefault(StorageUtility.MaxTakeCount);
            return _store
                .GetTableItems(filter.Compile())
                .Chunk(maxPerPage.GetValueOrDefault(maxPerPageValue))
                .Select((x, i) => Page<TableItem>.FromValues(
                    x,
                    continuationToken: x.Length == maxPerPageValue ? $"table-item-page-{i}" : null,
                    new MemoryResponse(HttpStatusCode.OK)));
        }

        private Response DeleteTableResponse(string tableName)
        {
            var result = _store.DeleteTable(tableName);
            return result switch
            {
                StorageResultType.Success => new MemoryResponse(HttpStatusCode.NoContent),
                StorageResultType.DoesNotExist => new MemoryResponse(HttpStatusCode.NotFound),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }
    }
}

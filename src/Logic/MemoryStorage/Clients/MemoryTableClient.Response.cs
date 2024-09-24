// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryTableClient
    {
        private Response<TableItem> CreateIfNotExistsResponse()
        {
            var result = Store.CreateIfNotExists();
            return result.Type switch
            {
                StorageResultType.Success => Response.FromValue(result.Value, new MemoryResponse(HttpStatusCode.Created)),
                StorageResultType.AlreadyExists => Response.FromValue(result.Value, new MemoryResponse(HttpStatusCode.NoContent)),
                _ => throw new NotImplementedException("Unexpected result type: " + result.Type),
            };
        }

        private Response<IReadOnlyList<Response>> SubmitTransactionResponse(
            IEnumerable<TableTransactionAction> transactionActions)
        {
            var transactionResult = Store.SubmitTransaction(transactionActions.ToList());

            for (var i = 0; i < transactionResult.Value.Count; i++)
            {
                var entityResult = transactionResult.Value[i];
                if (entityResult.Type == StorageResultType.Success)
                {
                    continue;
                }

                var ex = GetErrorException(entityResult);
                ex.Data.Add("FailedEntity", i.ToString(CultureInfo.InvariantCulture));
                throw new TableTransactionFailedException(ex);
            }

            IReadOnlyList<Response> responses = transactionResult
                .Value
                .Select(x => new MemoryResponse(HttpStatusCode.NoContent, x.Value))
                .ToList();

            return Response.FromValue(responses, new MemoryResponse(HttpStatusCode.Accepted));
        }

        private IEnumerable<Page<T>> GetEntityPages<T>(
            string? filter,
            int? maxPerPage,
            IEnumerable<string>? select) where T : ITableEntity
        {
            if (filter is not null)
            {
                throw new NotSupportedException();
            }

            return GetEntityPages<T>(filter: x => true, maxPerPage, select);
        }

        private IEnumerable<Page<T>> GetEntityPages<T>(
            Expression<Func<T, bool>> filter,
            int? maxPerPage,
            IEnumerable<string>? select) where T : ITableEntity
        {
            if (maxPerPage.HasValue && (maxPerPage.Value < 1 || maxPerPage.Value > StorageUtility.MaxTakeCount))
            {
                throw new ArgumentOutOfRangeException(nameof(maxPerPage));
            }

            var result = Store.GetEntities(filter.Compile(), select?.ToList());
            if (result.Type != StorageResultType.Success)
            {
                throw result.Type switch
                {
                    StorageResultType.DoesNotExist => new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                    _ => new NotImplementedException("Unexpected result type: " + result.Type),
                };
            }

            var maxPerPageValue = maxPerPage.GetValueOrDefault(StorageUtility.MaxTakeCount);
            return result
                .Value
                .Chunk(maxPerPageValue)
                .Select((x, i) => Page<T>.FromValues(
                    x,
                    continuationToken: x.Length == maxPerPageValue ? $"table-entity-page-{i}" : null,
                    new MemoryResponse(HttpStatusCode.OK)));
        }

        private Response DeleteResponse()
        {
            var result = Store.Delete();
            return result switch
            {
                StorageResultType.Success => new MemoryResponse(HttpStatusCode.Created),
                StorageResultType.DoesNotExist => new MemoryResponse(HttpStatusCode.NotFound),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }

        private Response AddEntityResponse(ITableEntity entity)
        {
            return SubmitSingleAction(new TableTransactionAction(
                TableTransactionActionType.Add,
                entity), allowNotFound: false);
        }

        private Response DeleteEntityResponse(
            string partitionKey,
            string rowKey,
            ETag ifMatch = default)
        {
            return SubmitSingleAction(new TableTransactionAction(
                TableTransactionActionType.Delete,
                new TableEntity(partitionKey, rowKey),
                ifMatch == default ? ETag.All : ifMatch), allowNotFound: true);
        }

        private Response UpdateEntityResponse(
            ITableEntity entity,
            ETag ifMatch,
            TableUpdateMode mode = TableUpdateMode.Merge)
        {
            return SubmitSingleAction(new TableTransactionAction(
                mode switch
                {
                    TableUpdateMode.Replace => TableTransactionActionType.UpdateReplace,
                    TableUpdateMode.Merge => TableTransactionActionType.UpdateMerge,
                    _ => throw new NotImplementedException("Unexpected table update mode: " + mode),
                },
                entity,
                ifMatch), allowNotFound: false);
        }

        private Response UpsertEntityResponse(
            ITableEntity entity,
            TableUpdateMode mode = TableUpdateMode.Merge)
        {
            return SubmitSingleAction(new TableTransactionAction(
                mode switch
                {
                    TableUpdateMode.Replace => TableTransactionActionType.UpsertReplace,
                    TableUpdateMode.Merge => TableTransactionActionType.UpsertMerge,
                    _ => throw new NotImplementedException("Unexpected table update mode: " + mode),
                },
                entity), allowNotFound: false);
        }

        private Response<T> GetEntityResponse<T>(
            string partitionKey,
            string rowKey,
            IEnumerable<string>? select = null) where T : ITableEntity
        {
            var result = Store.GetEntity<T>(partitionKey, rowKey, select?.ToList());
            return result.Type switch
            {
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => Response.FromValue(result.Value, new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result.Type),
            };
        }

        private List<StorageResult<ETag?>> SubmitTransactionOrThrow(IReadOnlyList<TableTransactionAction> transactionActions)
        {
            var transactionResult = Store.SubmitTransaction(transactionActions);
            if (transactionResult.Type != StorageResultType.Success)
            {
                throw transactionResult.Type switch
                {
                    StorageResultType.DoesNotExist => new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                    _ => new NotImplementedException("Unexpected result type: " + transactionResult.Type),
                };
            }

            return transactionResult.Value;
        }

        private static RequestFailedException GetErrorException(StorageResult<ETag?> entityResult)
        {
            var (status, errorCode) = entityResult.Type switch
            {
                StorageResultType.ETagMismatch => (HttpStatusCode.PreconditionFailed, TableErrorCode.UpdateConditionNotSatisfied),
                StorageResultType.DoesNotExist => (HttpStatusCode.NotFound, TableErrorCode.EntityNotFound),
                StorageResultType.AlreadyExists => (HttpStatusCode.Conflict, TableErrorCode.EntityAlreadyExists),
                _ => throw new NotImplementedException("Unexpected result type: " + entityResult.Type),
            };

            return new RequestFailedException((int)status, errorCode.ToString(), errorCode.ToString(), innerException: null);
        }

        private Response SubmitSingleAction(TableTransactionAction action, bool allowNotFound)
        {
            var transactionResults = SubmitTransactionOrThrow([action]);
            var entityResult = transactionResults[0];

            if (allowNotFound && entityResult.Type == StorageResultType.DoesNotExist)
            {
                return new MemoryResponse(HttpStatusCode.NotFound);
            }

            if (entityResult.Type != StorageResultType.Success)
            {
                throw GetErrorException(entityResult);
            }

            return new MemoryResponse(HttpStatusCode.NoContent, entityResult.Value);
        }
    }
}

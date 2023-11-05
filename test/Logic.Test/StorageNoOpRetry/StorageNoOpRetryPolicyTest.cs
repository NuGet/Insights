// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.StorageNoOpRetry
{
    public class StorageNoOpRetryPolicyTest : BaseLogicIntegrationTest
    {
        public class TableStorageForEntity : StorageNoOpRetryPolicyTest
        {
            [Fact]
            public async Task DoesNotSwallowConflictWhenAnotherThreadAdded()
            {
                // Arrange
                var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
                var table = tableServiceClient.GetTableClient(ContainerName);
                await table.CreateIfNotExistsAsync();

                var fasterAdd = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rk", MyProperty = "bar" };

                var requestCount = 0;
                HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.Method == HttpMethod.Post && r.RequestUri.AbsolutePath.EndsWith(ContainerName, StringComparison.Ordinal))
                    {
                        requestCount++;
                        if (requestCount == 1)
                        {
                            var response = await table.AddEntityAsync(fasterAdd);
                            fasterAdd.UpdateETag(response);
                            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { RequestMessage = r };
                        }
                    }

                    return null;
                };

                // Act & Assert
                var slowerAdd = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rk", MyProperty = "foo" };
                var ex = await Assert.ThrowsAsync<RequestFailedException>(() => table.AddEntityAsync(slowerAdd));
                Assert.Equal((int)HttpStatusCode.Conflict, ex.Status);
                TableEntityWithClientRequestId finalEntity = await table.GetEntityAsync<TableEntityWithClientRequestId>(slowerAdd.PartitionKey, slowerAdd.RowKey);
                Assert.Equal("bar", finalEntity.MyProperty);
                Assert.Equal(fasterAdd.ETag, finalEntity.ETag);
                Assert.Equal(fasterAdd.ClientRequestId, finalEntity.ClientRequestId);

                Assert.Equal("foo", slowerAdd.MyProperty);
                Assert.NotEqual(slowerAdd.ETag, finalEntity.ETag);
                Assert.NotEqual(slowerAdd.ClientRequestId, finalEntity.ClientRequestId);
            }

            [Fact]
            public async Task SwallowsConflictForAdd()
            {
                // Arrange
                var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
                var table = tableServiceClient.GetTableClient(ContainerName);
                await table.CreateIfNotExistsAsync();

                var requestCount = 0;
                HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.Method == HttpMethod.Post && r.RequestUri.AbsolutePath.EndsWith(ContainerName, StringComparison.Ordinal))
                    {
                        requestCount++;
                        if (requestCount == 1)
                        {
                            await b(r, t);
                            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { RequestMessage = r };
                        }
                    }

                    return null;
                };

                // Act
                var entity = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rk", MyProperty = "foo" };
                var addResponse = await table.AddEntityAsync(entity);
                entity.UpdateETag(addResponse);
                var etagBeforeA = entity.ETag;
                var idBeforeA = entity.ClientRequestId;

                // Assert
                TableEntityWithClientRequestId finalEntity = await table.GetEntityAsync<TableEntityWithClientRequestId>(entity.PartitionKey, entity.RowKey);
                Assert.Equal(etagBeforeA, finalEntity.ETag);
                Assert.Equal(idBeforeA, finalEntity.ClientRequestId);
            }

            [Fact]
            public async Task DoesNotSwallowConflictWhenAnotherThreadUpdated()
            {
                // Arrange
                var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
                var table = tableServiceClient.GetTableClient(ContainerName);
                await table.CreateIfNotExistsAsync();

                var slowerUpdate = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rk", MyProperty = "foo" };
                var addResponse = await table.AddEntityAsync(slowerUpdate);
                slowerUpdate.UpdateETag(addResponse);
                var etagBeforeA = slowerUpdate.ETag;

                var fasterUpdate = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rk", MyProperty = "baz" };

                var requestCount = 0;
                HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.Method == HttpMethod.Put && r.RequestUri.AbsolutePath.EndsWith("(PartitionKey='pk',RowKey='rk')", StringComparison.Ordinal))
                    {
                        requestCount++;
                        if (requestCount == 1)
                        {
                            var response = await table.UpdateEntityAsync(fasterUpdate, etagBeforeA, TableUpdateMode.Replace);
                            fasterUpdate.UpdateETag(response);

                            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { RequestMessage = r };
                        }
                    }

                    return null;
                };

                // Act & Assert
                slowerUpdate.MyProperty = "bar";
                var ex = await Assert.ThrowsAsync<RequestFailedException>(
                    () => table.UpdateEntityAsync(slowerUpdate, slowerUpdate.ETag, mode: TableUpdateMode.Replace));

                TableEntityWithClientRequestId finalEntity = await table.GetEntityAsync<TableEntityWithClientRequestId>(slowerUpdate.PartitionKey, slowerUpdate.RowKey);
                Assert.Equal("baz", finalEntity.MyProperty);
                Assert.Equal(fasterUpdate.ETag, finalEntity.ETag);
                Assert.Equal(fasterUpdate.ClientRequestId, finalEntity.ClientRequestId);
                Assert.NotEqual(slowerUpdate.ETag, finalEntity.ETag);
                Assert.NotEqual(slowerUpdate.ClientRequestId, finalEntity.ClientRequestId);
            }

            [Fact]
            public async Task SwallowsConflictForUpdate()
            {
                // Arrange
                var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
                var table = tableServiceClient.GetTableClient(ContainerName);
                await table.CreateIfNotExistsAsync();

                var requestCount = 0;
                HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.Method == HttpMethod.Put && r.RequestUri.AbsolutePath.EndsWith("(PartitionKey='pk',RowKey='rk')", StringComparison.Ordinal))
                    {
                        requestCount++;
                        if (requestCount == 1)
                        {
                            await b(r, t);
                            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { RequestMessage = r };
                        }
                    }

                    return null;
                };

                var entity = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rk", MyProperty = "foo" };
                var addResponse = await table.AddEntityAsync(entity);
                entity.UpdateETag(addResponse);
                var etagBeforeA = entity.ETag;
                var idBeforeA = entity.ClientRequestId;

                // Act
                entity.MyProperty = "bar";
                var updateResponse = await table.UpdateEntityAsync(entity, entity.ETag, mode: TableUpdateMode.Replace);
                entity.UpdateETag(updateResponse);

                // Assert
                TableEntityWithClientRequestId finalEntity = await table.GetEntityAsync<TableEntityWithClientRequestId>(entity.PartitionKey, entity.RowKey);
                Assert.Equal(entity.ETag, finalEntity.ETag);
                Assert.Equal(entity.ClientRequestId, finalEntity.ClientRequestId);
                Assert.NotEqual(etagBeforeA, entity.ETag);
                Assert.NotEqual(idBeforeA, entity.ClientRequestId);
            }

            public TableStorageForEntity(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public class TableStorageForBatch : StorageNoOpRetryPolicyTest
        {
            [Fact]
            public async Task SwallowsConflictForSmallBatchUpdate()
            {
                // Arrange
                var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
                var table = tableServiceClient.GetTableClient(ContainerName);
                await table.CreateIfNotExistsAsync();

                var requestCount = 0;
                HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.Method == HttpMethod.Post && r.RequestUri.AbsolutePath.EndsWith("$batch", StringComparison.Ordinal))
                    {
                        requestCount++;
                        if (requestCount == 1)
                        {
                            var response = await b(r, t);
                            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { RequestMessage = r };
                        }
                    }

                    return null;
                };

                var entityA = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rkA", MyProperty = "foo" };
                var entityB = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rkB", MyProperty = "foo" };
                var responseA = await table.AddEntityAsync(entityA);
                entityA.UpdateETag(responseA);
                var responseB = await table.AddEntityAsync(entityB);
                entityB.UpdateETag(responseB);

                var etagBeforeA = entityA.ETag;
                var idBeforeA = entityA.ClientRequestId;
                var etagBeforeB = entityB.ETag;
                var idBeforeB = entityB.ClientRequestId;

                // Act
                entityA.MyProperty = "bar";
                entityB.MyProperty = "bar";
                var responses = await table.SubmitTransactionAsync(
                    new[]
                    {
                        new TableTransactionAction(TableTransactionActionType.UpdateReplace, entityA, entityA.ETag),
                        new TableTransactionAction(TableTransactionActionType.UpdateReplace, entityB, entityB.ETag),
                    });
                entityA.UpdateETag(responses.Value[0]);
                entityB.UpdateETag(responses.Value[1]);

                // Assert
                TableEntityWithClientRequestId finalEntityA = await table.GetEntityAsync<TableEntityWithClientRequestId>(entityA.PartitionKey, entityA.RowKey);
                TableEntityWithClientRequestId finalEntityB = await table.GetEntityAsync<TableEntityWithClientRequestId>(entityB.PartitionKey, entityB.RowKey);
                Assert.Equal(entityA.ETag, finalEntityA.ETag);
                Assert.Equal(entityA.ClientRequestId, finalEntityA.ClientRequestId);
                Assert.Equal(entityB.ETag, finalEntityB.ETag);
                Assert.Equal(entityB.ClientRequestId, finalEntityB.ClientRequestId);
                Assert.NotEqual(etagBeforeA, entityA.ETag);
                Assert.NotEqual(idBeforeA, entityA.ClientRequestId);
                Assert.NotEqual(etagBeforeB, entityB.ETag);
                Assert.NotEqual(idBeforeB, entityB.ClientRequestId);
            }

            [Fact]
            public async Task SwallowsConflictWhenAnotherThreadUpdatedSomeEntities()
            {
                // Arrange
                FailFastLogLevel = LogLevel.Error;
                AssertLogLevel = LogLevel.Error;

                var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
                var table = tableServiceClient.GetTableClient(ContainerName);
                await table.CreateIfNotExistsAsync();

                var entityA = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rkA", MyProperty = "foo" };
                var entityB = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rkB", MyProperty = "foo" };
                var responseA = await table.AddEntityAsync(entityA);
                entityA.UpdateETag(responseA);
                var responseB = await table.AddEntityAsync(entityB);
                entityB.UpdateETag(responseB);

                var fasterUpdateA = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rkA", MyProperty = "baz" };

                var requestCount = 0;
                HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.Method == HttpMethod.Post && r.RequestUri.AbsolutePath.EndsWith("$batch", StringComparison.Ordinal))
                    {
                        requestCount++;
                        if (requestCount == 1)
                        {
                            await b(r, t);

                            var responseA = await table.UpdateEntityAsync(fasterUpdateA, ETag.All, TableUpdateMode.Replace);
                            fasterUpdateA.UpdateETag(responseA);

                            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { RequestMessage = r };
                        }
                    }

                    return null;
                };

                var etagBeforeA = entityA.ETag;
                var idBeforeA = entityA.ClientRequestId;
                var etagBeforeB = entityB.ETag;
                var idBeforeB = entityB.ClientRequestId;

                // Act
                entityA.MyProperty = "bar";
                entityB.MyProperty = "bar";
                var responses = await table.SubmitTransactionAsync(
                    new[]
                    {
                        new TableTransactionAction(TableTransactionActionType.UpdateReplace, entityA, entityA.ETag),
                        new TableTransactionAction(TableTransactionActionType.UpdateReplace, entityB, entityB.ETag),
                    });
                entityA.UpdateETag(responses.Value[0]);
                entityB.UpdateETag(responses.Value[1]);

                // Assert
                TableEntityWithClientRequestId finalEntityA = await table.GetEntityAsync<TableEntityWithClientRequestId>(entityA.PartitionKey, entityA.RowKey);
                TableEntityWithClientRequestId finalEntityB = await table.GetEntityAsync<TableEntityWithClientRequestId>(entityB.PartitionKey, entityB.RowKey);
                Assert.Equal("bar", entityA.MyProperty);
                Assert.Equal("bar", entityB.MyProperty);
                Assert.Equal("baz", finalEntityA.MyProperty);
                Assert.Equal("bar", finalEntityB.MyProperty);
                Assert.Equal(StorageNoOpRetryPolicy.StaleETag, entityA.ETag.ToString("H"));
                Assert.Equal(fasterUpdateA.ETag, finalEntityA.ETag);
                Assert.Equal(entityA.ClientRequestId, finalEntityB.ClientRequestId); // the previous client request ID is returned
                Assert.Equal(entityB.ETag, finalEntityB.ETag);
                Assert.Equal(entityB.ClientRequestId, finalEntityB.ClientRequestId);
                Assert.NotEqual(etagBeforeA, entityA.ETag);
                Assert.NotEqual(idBeforeA, entityA.ClientRequestId);
                Assert.NotEqual(etagBeforeB, entityB.ETag);
                Assert.NotEqual(idBeforeB, entityB.ClientRequestId);
            }

            [Fact]
            public async Task SwallowsConflictWhenAnotherThreadDeletedSomeEntities()
            {
                // Arrange
                FailFastLogLevel = LogLevel.Error;
                AssertLogLevel = LogLevel.Error;

                var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
                var table = tableServiceClient.GetTableClient(ContainerName);
                await table.CreateIfNotExistsAsync();

                var entityA = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rkA", MyProperty = "foo" };
                var entityB = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rkB", MyProperty = "foo" };
                var responseA = await table.AddEntityAsync(entityA);
                entityA.UpdateETag(responseA);
                var responseB = await table.AddEntityAsync(entityB);
                entityB.UpdateETag(responseB);

                var requestCount = 0;
                HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.Method == HttpMethod.Post && r.RequestUri.AbsolutePath.EndsWith("$batch", StringComparison.Ordinal))
                    {
                        requestCount++;
                        if (requestCount == 1)
                        {
                            await b(r, t);

                            await table.DeleteEntityAsync(entityA.PartitionKey, entityA.RowKey);

                            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { RequestMessage = r };
                        }
                    }

                    return null;
                };

                var etagBeforeA = entityA.ETag;
                var idBeforeA = entityA.ClientRequestId;
                var etagBeforeB = entityB.ETag;
                var idBeforeB = entityB.ClientRequestId;

                // Act
                entityA.MyProperty = "bar";
                entityB.MyProperty = "bar";
                var responses = await table.SubmitTransactionAsync(
                    new[]
                    {
                        new TableTransactionAction(TableTransactionActionType.UpdateReplace, entityA, entityA.ETag),
                        new TableTransactionAction(TableTransactionActionType.UpdateReplace, entityB, entityB.ETag),
                    });
                entityA.UpdateETag(responses.Value[0]);
                entityB.UpdateETag(responses.Value[1]);

                // Assert
                TableEntityWithClientRequestId finalEntityB = await table.GetEntityAsync<TableEntityWithClientRequestId>(entityB.PartitionKey, entityB.RowKey);
                Assert.Equal("bar", entityA.MyProperty);
                Assert.Equal("bar", entityB.MyProperty);
                var ex = await Assert.ThrowsAsync<RequestFailedException>(() => table.GetEntityAsync<TableEntityWithClientRequestId>(entityA.PartitionKey, entityA.RowKey));
                Assert.Equal(HttpStatusCode.NotFound, (HttpStatusCode)ex.Status);
                Assert.Equal("bar", finalEntityB.MyProperty);
                Assert.Equal(StorageNoOpRetryPolicy.StaleETag, entityA.ETag.ToString("H"));
                Assert.Equal(entityA.ClientRequestId, finalEntityB.ClientRequestId); // the previous client request ID is returned
                Assert.Equal(entityB.ETag, finalEntityB.ETag);
                Assert.Equal(entityB.ClientRequestId, finalEntityB.ClientRequestId);
                Assert.NotEqual(etagBeforeA, entityA.ETag);
                Assert.NotEqual(idBeforeA, entityA.ClientRequestId);
                Assert.NotEqual(etagBeforeB, entityB.ETag);
                Assert.NotEqual(idBeforeB, entityB.ClientRequestId);
            }

            [Fact]
            public async Task DoesNotSwallowConflictWhenAnotherThreadUpdatedAllEntities()
            {
                // Arrange
                var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
                var table = tableServiceClient.GetTableClient(ContainerName);
                await table.CreateIfNotExistsAsync();

                var slowerUpdateA = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rkA", MyProperty = "foo" };
                var slowerUpdateB = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rkB", MyProperty = "foo" };
                var responseA = await table.AddEntityAsync(slowerUpdateA);
                slowerUpdateA.UpdateETag(responseA);
                var responseB = await table.AddEntityAsync(slowerUpdateB);
                slowerUpdateB.UpdateETag(responseB);

                var fasterUpdateA = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rkA", MyProperty = "baz" };
                var fasterUpdateB = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rkB", MyProperty = "baz" };

                var requestCount = 0;
                HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.Method == HttpMethod.Post && r.RequestUri.AbsolutePath.EndsWith("$batch", StringComparison.Ordinal))
                    {
                        requestCount++;
                        if (requestCount == 1)
                        {
                            await b(r, t);

                            var responseA = await table.UpdateEntityAsync(fasterUpdateA, ETag.All, TableUpdateMode.Replace);
                            fasterUpdateA.UpdateETag(responseA);
                            var responseB = await table.UpdateEntityAsync(fasterUpdateB, ETag.All, TableUpdateMode.Replace);
                            fasterUpdateB.UpdateETag(responseB);

                            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { RequestMessage = r };
                        }
                    }

                    return null;
                };

                // Act & Assert
                slowerUpdateA.MyProperty = "bar";
                slowerUpdateB.MyProperty = "bar";
                var ex = await Assert.ThrowsAsync<TableTransactionFailedException>(
                    () => table.SubmitTransactionAsync(
                    new[]
                    {
                        new TableTransactionAction(TableTransactionActionType.UpdateReplace, slowerUpdateA, slowerUpdateA.ETag),
                        new TableTransactionAction(TableTransactionActionType.UpdateReplace, slowerUpdateB, slowerUpdateB.ETag),
                    }));

                Assert.Equal(0, ex.FailedTransactionActionIndex);
                TableEntityWithClientRequestId finalEntityA = await table.GetEntityAsync<TableEntityWithClientRequestId>(slowerUpdateA.PartitionKey, slowerUpdateA.RowKey);
                TableEntityWithClientRequestId finalEntityB = await table.GetEntityAsync<TableEntityWithClientRequestId>(slowerUpdateB.PartitionKey, slowerUpdateB.RowKey);
                Assert.Equal("baz", finalEntityA.MyProperty);
                Assert.Equal("baz", finalEntityB.MyProperty);
                Assert.Equal(fasterUpdateA.ETag, finalEntityA.ETag);
                Assert.Equal(fasterUpdateB.ETag, finalEntityB.ETag);
                Assert.Equal(fasterUpdateA.ClientRequestId, finalEntityA.ClientRequestId);
                Assert.Equal(fasterUpdateB.ClientRequestId, finalEntityB.ClientRequestId);
                Assert.NotEqual(slowerUpdateA.ETag, finalEntityA.ETag);
                Assert.NotEqual(slowerUpdateB.ETag, finalEntityB.ETag);
                Assert.NotEqual(slowerUpdateA.ClientRequestId, finalEntityA.ClientRequestId);
                Assert.NotEqual(slowerUpdateB.ClientRequestId, finalEntityB.ClientRequestId);
            }

            [Fact]
            public async Task DoesNotSwallowConflictWhenAnotherThreadDeletedAllEntities()
            {
                // Arrange
                var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
                var table = tableServiceClient.GetTableClient(ContainerName);
                await table.CreateIfNotExistsAsync();

                var slowerUpdateA = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rkA", MyProperty = "foo" };
                var slowerUpdateB = new TableEntityWithClientRequestId { PartitionKey = "pk", RowKey = "rkB", MyProperty = "foo" };
                var responseA = await table.AddEntityAsync(slowerUpdateA);
                slowerUpdateA.UpdateETag(responseA);
                var responseB = await table.AddEntityAsync(slowerUpdateB);
                slowerUpdateB.UpdateETag(responseB);

                var requestCount = 0;
                HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.Method == HttpMethod.Post && r.RequestUri.AbsolutePath.EndsWith("$batch", StringComparison.Ordinal))
                    {
                        requestCount++;
                        if (requestCount == 1)
                        {
                            await b(r, t);
                            await table.DeleteEntityAsync(slowerUpdateA.PartitionKey, slowerUpdateA.RowKey);
                            await table.DeleteEntityAsync(slowerUpdateB.PartitionKey, slowerUpdateB.RowKey);

                            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { RequestMessage = r };
                        }
                    }

                    return null;
                };

                // Act & Assert
                slowerUpdateA.MyProperty = "bar";
                slowerUpdateB.MyProperty = "bar";
                var ex = await Assert.ThrowsAsync<TableTransactionFailedException>(
                    () => table.SubmitTransactionAsync(
                    new[]
                    {
                        new TableTransactionAction(TableTransactionActionType.UpdateReplace, slowerUpdateA, slowerUpdateA.ETag),
                        new TableTransactionAction(TableTransactionActionType.UpdateReplace, slowerUpdateB, slowerUpdateB.ETag),
                    }));

                Assert.Equal(0, ex.FailedTransactionActionIndex);
                var entities = await table.QueryAsync<TableEntityWithClientRequestId>().ToListAsync();
                Assert.Empty(entities);
            }

            [Fact]
            public async Task SwallowsConflictForLargeBatchUpdate()
            {
                // Arrange
                var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
                var table = tableServiceClient.GetTableClient(ContainerName);
                await table.CreateIfNotExistsAsync();

                var allEntities = Enumerable.Range(0, 2000).Select(i => new TableEntityWithClientRequestId
                {
                    PartitionKey = "pk",
                    RowKey = "rk" + i.ToString("D4"),
                    MyProperty = "foo",
                }).ToList();
                var mutableBatch = new MutableTableTransactionalBatch(table);
                foreach (var entity in allEntities)
                {
                    mutableBatch.AddEntity(entity);
                    await mutableBatch.SubmitBatchIfFullAsync();
                }

                await mutableBatch.SubmitBatchIfNotEmptyAsync();

                var requestCount = 0;
                HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.Method == HttpMethod.Post && r.RequestUri.AbsolutePath.EndsWith("$batch", StringComparison.Ordinal))
                    {
                        requestCount++;
                        if (requestCount == 1)
                        {
                            var response = await b(r, t);
                            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { RequestMessage = r };
                        }
                    }

                    return null;
                };

                // Act
                var testEntities = Enumerable
                    .Empty<TableEntityWithClientRequestId>()
                    .Concat(Enumerable.Range(0, 11).Select(i => allEntities[i * 2])) // 0, 2, 4, ... 20
                    .Concat(Enumerable.Range(0, 11).Select(i => allEntities[1500 + i * 2])) // 1500, 1502, 1504, ... 1520
                    .ToList();

                foreach (var entity in testEntities)
                {
                    entity.MyProperty = "bar";
                }

                var responses = await table.SubmitTransactionAsync(
                    testEntities.Select(e => new TableTransactionAction(TableTransactionActionType.UpdateReplace, e, e.ETag)));
                for (var i = 0; i < responses.Value.Count; i++)
                {
                    testEntities[i].UpdateETag(responses.Value[i]);
                }

                // Assert
                var rowKeyToFinalEntity = await table.QueryAsync<TableEntityWithClientRequestId>().ToDictionaryAsync(x => x.RowKey);
                Assert.All(testEntities, e => Assert.Equal("bar", rowKeyToFinalEntity[e.RowKey].MyProperty));
                Assert.All(testEntities, e => Assert.NotNull(e.ClientRequestId));
                Assert.All(testEntities, e => Assert.Equal(e.ClientRequestId, rowKeyToFinalEntity[e.RowKey].ClientRequestId));
                Assert.All(testEntities, e => Assert.NotEqual(default, e.ETag));
                Assert.All(testEntities, e => Assert.Equal(e.ETag, rowKeyToFinalEntity[e.RowKey].ETag));
            }

            [Fact]
            public async Task SwallowsConflictForLargeBatchUpdateWhenAnotherThreadDeletedSomeEntities()
            {
                // Arrange
                FailFastLogLevel = LogLevel.Error;
                AssertLogLevel = LogLevel.Error;

                var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
                var table = tableServiceClient.GetTableClient(ContainerName);
                await table.CreateIfNotExistsAsync();

                var allEntities = Enumerable.Range(0, 2000).Select(i => new TableEntityWithClientRequestId
                {
                    PartitionKey = "pk",
                    RowKey = "rk" + i.ToString("D4"),
                    MyProperty = "foo",
                }).ToList();
                var mutableBatch = new MutableTableTransactionalBatch(table);
                foreach (var entity in allEntities)
                {
                    mutableBatch.AddEntity(entity);
                    await mutableBatch.SubmitBatchIfFullAsync();
                }

                await mutableBatch.SubmitBatchIfNotEmptyAsync();

                var requestCount = 0;
                HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.Method == HttpMethod.Post && r.RequestUri.AbsolutePath.EndsWith("$batch", StringComparison.Ordinal))
                    {
                        requestCount++;
                        if (requestCount == 1)
                        {
                            var response = await b(r, t);

                            await table.DeleteEntityAsync(allEntities[4].PartitionKey, allEntities[4].RowKey); // in first page of batch
                            await table.DeleteEntityAsync(allEntities[5].PartitionKey, allEntities[5].RowKey); // not in batch
                            await table.DeleteEntityAsync(allEntities[6].PartitionKey, allEntities[6].RowKey); // in first page of batch

                            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { RequestMessage = r };
                        }
                    }

                    return null;
                };

                // Act
                var testEntities = Enumerable
                    .Empty<TableEntityWithClientRequestId>()
                    .Concat(Enumerable.Range(0, 11).Select(i => allEntities[i * 2])) // 0, 2, 4, ... 20
                    .Concat(Enumerable.Range(0, 11).Select(i => allEntities[1500 + i * 2])) // 1500, 1502, 1504, ... 1520
                    .ToList();

                foreach (var entity in testEntities)
                {
                    entity.MyProperty = "bar";
                }

                var responses = await table.SubmitTransactionAsync(
                    testEntities.Select(e => new TableTransactionAction(TableTransactionActionType.UpdateReplace, e, e.ETag)));
                for (var i = 0; i < responses.Value.Count; i++)
                {
                    testEntities[i].UpdateETag(responses.Value[i]);
                }

                // Assert
                var rowKeyToFinalEntity = await table.QueryAsync<TableEntityWithClientRequestId>().ToDictionaryAsync(x => x.RowKey);
                var lookup = testEntities.ToLookup(x => x.RowKey != "rk0004" && x.RowKey != "rk0006");
                var success = lookup[true];
                var deleted = lookup[false];
                Assert.All(success, e => Assert.Equal("bar", rowKeyToFinalEntity[e.RowKey].MyProperty));
                Assert.All(success, e => Assert.Equal(e.ClientRequestId, rowKeyToFinalEntity[e.RowKey].ClientRequestId));
                Assert.All(success, e => Assert.Equal(e.ETag, rowKeyToFinalEntity[e.RowKey].ETag));
                Assert.All(deleted, e => Assert.DoesNotContain(e.RowKey, rowKeyToFinalEntity.Keys));
                Assert.All(deleted, e => Assert.Equal(e.ClientRequestId, success.First().ClientRequestId));
                Assert.All(deleted, e => Assert.Equal(StorageNoOpRetryPolicy.StaleETag, e.ETag.ToString("H")));

                Assert.All(testEntities, e => Assert.NotNull(e.ClientRequestId));
                Assert.All(testEntities, e => Assert.NotEqual(default, e.ETag));

                Assert.Contains(LogMessages, m => m.Contains("Fetched 1000 entities with row keys between 'rk0000' and 'rk1002', with 9 matched and 2 missed."));
                Assert.Contains(LogMessages, m => m.Contains("The action at index 2 for partition key 'pk' and row key 'rk0004' has been superceded. Using a stale etag for the multipart/mixed response."));
                Assert.Contains(LogMessages, m => m.Contains("The action at index 3 for partition key 'pk' and row key 'rk0006' has been superceded. Using a stale etag for the multipart/mixed response."));
            }

            public TableStorageForBatch(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public StorageNoOpRetryPolicyTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            ContainerName = StoragePrefix + "1r1";
        }

        public string ContainerName { get; }

        private class TableEntityWithClientRequestId : ITableEntityWithClientRequestId
        {
            public string PartitionKey { get; set; }
            public string RowKey { get; set; }
            public DateTimeOffset? Timestamp { get; set; }
            public ETag ETag { get; set; }

            public string MyProperty { get; set; }
            public Guid? ClientRequestId { get; set; }
        }
    }
}

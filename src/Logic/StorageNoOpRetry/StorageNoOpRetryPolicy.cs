// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;

#nullable enable

namespace NuGet.Insights.StorageNoOpRetry
{
    public class StorageNoOpRetryPolicy : RetryPolicy
    {
        private static readonly Encoding UTF8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly ResponseClassifier RetryAllClassifier = new StatusCodeClassifier(Array.Empty<ushort>());
        private static readonly ConcurrentDictionary<string, IReadOnlyList<string>> ColumnNameToEntityColumnArray = new();
        private static readonly ConcurrentDictionary<string, IReadOnlyList<string>> ColumnNameToQueryColumnArray = new();

        public const string ClientRequestIdKey = "x-ms-client-request-id"; // scope property name expected by the Azure SDK
        public const string ClientRequestIdHeader = "x-ms-client-request-id"; // header used by Azure Store
        public const string RetryContextKey = "RetryContext";
        public const string ETagHeader = "ETag";
        public const string StaleETag = "W/\"datetime'2000-01-01T00%3A00%3A00.000000Z'\"";

        private readonly ILogger<StorageNoOpRetryPolicy> _logger;

        public StorageNoOpRetryPolicy(
            ILogger<StorageNoOpRetryPolicy> logger,
            int maxRetries = 3,
            DelayStrategy? delayStrategy = null) : base(maxRetries, delayStrategy)
        {
            _logger = logger;
        }

        public static IDisposable CreateScope(RetryContext retryContext)
        {
            var scopeProperties = new Dictionary<string, object?>()
            {
                // This is not strictly necessary but it aligns access logs with the value seen on the entity, which is nice.
                { ClientRequestIdKey, retryContext.ClientRequestIdString },

                // These are used to perform a read on the entity during conflict handling to no-op some cases.
                { RetryContextKey, retryContext },
            };

            return HttpPipeline.CreateHttpMessagePropertiesScope(scopeProperties);
        }

        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            // Ideally this would be blocked with a NotImplementedException, but we have some sync paths still
            // https://github.com/azure/azure-sdk-for-net/issues/35548
            base.Process(message, pipeline);
        }

        public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            if (TryGetContext(message, out RetryContext? context)
                && context is TableRetryBatchContext)
            {
                message.ResponseClassifier = RetryAllClassifier;
            }

            return base.ProcessAsync(message, pipeline);
        }

        protected override ValueTask OnSendingRequestAsync(HttpMessage message)
        {
            if (TryGetContext(message, out RetryContext? context))
            {
                context.Attempts++;
            }

            return base.OnSendingRequestAsync(message);
        }

        private static bool TryGetContext<T>(HttpMessage message, [NotNullWhen(returnValue: true)] out T? context)
        {
            if (message.TryGetProperty(RetryContextKey, out var contextObj)
                && contextObj is T contextMatch)
            {
                context = contextMatch;
                return true;
            }

            context = default;
            return false;
        }

        protected override async ValueTask<bool> ShouldRetryAsync(HttpMessage message, Exception? exception)
        {
            var shouldRetry = await base.ShouldRetryAsync(message, exception);

            if (message.HasResponse
                && exception is null
                && TryGetContext(message, out RetryContext? context)
                && context.Attempts >= 2)
            {
                _logger.LogTransientWarning(
                    "Storage no-op retry policy has begun. Attempts: {Attempts}. Client request ID: {ClientRequestId}.",
                    context.Attempts,
                    context.ClientRequestId);

                var isSuccess = context switch
                {
                    TableRetryEntityContext c => await TrySetSuccessForTableEntityAsync(shouldRetry, message, c),
                    TableRetryBatchContext c => await TrySetSuccessForTableBatchAsync(shouldRetry, message, c),
                    _ => throw new NotImplementedException(),
                };

                if (isSuccess)
                {
                    return false;
                }
            }

            return shouldRetry;
        }

        private async Task<bool> TrySetSuccessForTableEntityAsync(bool shouldRetry, HttpMessage message, TableRetryEntityContext context)
        {
            _logger.LogTransientWarning(
                "Storage no-op retry policy is for an entity in table '{Table}' with partition key '{PartitionKey}' and row key '{RowKey}'.",
                context.Entity.PartitionKey,
                context.Entity.RowKey,
                context.Client.Name);

            if (shouldRetry)
            {
                _logger.LogInformation("Allowing default retry behavior for table entity request.");
                return false;
            }

            var method = message.Request.Method;
            var status = (HttpStatusCode)message.Response.Status;
            var isConflict =
                (method == RequestMethod.Put && status == HttpStatusCode.PreconditionFailed)
                || (method == RequestMethod.Post && status == HttpStatusCode.Conflict);

            var select = ColumnNameToEntityColumnArray.GetOrAdd(context.ClientRequestIdColumn, x => new[] { x });
            var remote = await GetRemoteEntityOrNullAsync(context, context.Entity.PartitionKey, context.Entity.RowKey);
            if (remote is null)
            {
                _logger.LogTransientWarning("No entity was found.");
                return false;
            }

            if (!remote.Value.TryGetValue(context.ClientRequestIdColumn, out var value)
                || value is not Guid remoteClientRequestId)
            {
                _logger.LogTransientWarning(
                    "No GUID client request ID was found on the remote entity. Found value: '{RemoteClientRequestId}'.",
                    value);
                return false;
            }

            var etag = remote.Value.ETag.ToString("H");
            if (string.IsNullOrEmpty(etag))
            {
                _logger.LogTransientWarning("No etag was found on the remote entity.");
                return false;
            }

            if (remoteClientRequestId != context.ClientRequestId)
            {
                _logger.LogTransientWarning(
                    "The client request ID did not match. Found value: '{RemoteClientRequestId}'.",
                    remoteClientRequestId);
                return false;
            }

            _logger.LogTransientWarning(
                "The client request ID matched on the entity. Spoofing an HTTP 204 No Content response with etag header '{ETag}'.",
                etag);

            message.Response.Dispose();
            message.Response = new StubResponse(
                (int)HttpStatusCode.NoContent,
                "No Content",
                context.ClientRequestIdString,
                new Dictionary<string, string>
                {
                    { ClientRequestIdHeader, context.ClientRequestIdString },
                    { ETagHeader, etag },
                });

            return true;
        }

        private async Task<Response<TableEntity>?> GetRemoteEntityOrNullAsync(TableRetryContext context, string partitionKey, string rowKey)
        {
            var select = ColumnNameToEntityColumnArray.GetOrAdd(context.ClientRequestIdColumn, x => new[] { x });

            try
            {
                using (CreateScope(new RetryContext(context)))
                {
                    return await context.Client.GetEntityAsync<TableEntity>(
                        partitionKey,
                        rowKey,
                        select);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        private async Task<bool> TrySetSuccessForTableBatchAsync(bool shouldRetry, HttpMessage message, TableRetryBatchContext context)
        {
            _logger.LogTransientWarning(
                "Storage no-op retry policy is for a table $batch request with {Count} action on table '{Table}'.",
                context.Actions.Count,
                context.Client.Name);

            if (message.Response.Status != (int)HttpStatusCode.Accepted)
            {
                _logger.LogTransientWarning("The response code was not HTTP 202 Accepted.");
                return false;
            }

            if (shouldRetry)
            {
                throw new NotImplementedException("The 202 Accepted status for a table batch request should not be considered retriable by the SDK.");
            }

            if (message.Response.ContentStream is null)
            {
                _logger.LogTransientWarning("No response body was returned.");
                return false;
            }

            if (!message.Response.Headers.TryGetValue("Content-Type", out var contentType)
                || contentType is null
                || !contentType.StartsWith("multipart/mixed", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogTransientWarning(
                    "The response did not have a mulitpart/mixed content type. Actual content type: {ContentType}.",
                    contentType);
                return false;
            }

            var bufferedContent = new MemoryStream();
            await message.Response.ContentStream.CopyToAsync(bufferedContent);
            message.Response.ContentStream = bufferedContent;

            bufferedContent.Position = 0;
            var responses = await MultipartResponse.ParseAsync(
                message.Response,
                expectCrLf: false,
                message.CancellationToken);
            bufferedContent.Position = 0;

            var failedResponse = responses.FirstOrDefault(x => x.Status >= (int)HttpStatusCode.BadRequest);
            if (failedResponse is null)
            {
                _logger.LogTransientWarning(
                    "No failed sub-responses were found. Sub-response count: {Count}. First status code: {StatusCode}.",
                    responses.Length,
                    responses.FirstOrDefault()?.Status);
                return false;
            }

            var requestFailedException = new RequestFailedException(failedResponse);
            var tableTransactionException = new TableTransactionFailedException(requestFailedException);
            var failedIndex = tableTransactionException.FailedTransactionActionIndex;
            if (!failedIndex.HasValue || failedIndex.Value >= context.Actions.Count)
            {
                _logger.LogTransientWarning(
                    "The failed transaction index did not have an expected value. Value: {Value}.",
                    failedIndex);
                return false;
            }

            var isPost = context.Actions[failedIndex.Value].ActionType == TableTransactionActionType.Add;
            var status = (HttpStatusCode)failedResponse.Status;
            var isConflict =
                (!isPost && status == HttpStatusCode.PreconditionFailed)
                || (isPost && status == HttpStatusCode.Conflict);

            var remoteEntities = await GetRemoteEntitiesAsync(context);

            var anyMatchingClientRequestId = remoteEntities
                .Values
                .Any(x => GetClientRequestId(context, x) == context.ClientRequestId);

            if (!anyMatchingClientRequestId)
            {
                _logger.LogTransientWarning("No entities in the batch have a matching client request.");
                return false;
            }

            _logger.LogTransientWarning("The client request ID matched on at least one entity. Spoofing a multipart response.");

            message.Response.Dispose();
            message.Response = GetMultipartResponse(context, remoteEntities);

            return true;
        }

        private async Task<Dictionary<string, TableEntity?>> GetRemoteEntitiesAsync(TableRetryBatchContext context)
        {
            var pk = context.TrackedEntities[0].PartitionKey;
            var trackedRowKeys = context.TrackedEntities.Select(x => x.RowKey).ToHashSet();
            var remaining = trackedRowKeys.Count;
            var output = new Dictionary<string, TableEntity?>();

            _logger.LogTransientWarning(
                "Fetching {Count} remote entities on partition key '{PartitionKey}'.",
                trackedRowKeys.Count,
                pk);

            // Try to fetch a bunch of the tracked entities all at once. At the time of writing this, queries are 19x
            // more costly (in USD) than single GET requests. We pick an arbitrary threshold to choose between using
            // queries or point reads to get the current remote state for N row keys. Remember than the number of row
            // keys here is limited on the upper bound by max transaction batch size which is 100 entities.
            if (context.TrackedEntities.Count > 10)
            {
                var rkMin = context.TrackedEntities.Select(x => x.RowKey).Min(StringComparer.Ordinal);
                var rkMax = context.TrackedEntities.Select(x => x.RowKey).Max(StringComparer.Ordinal);

                _logger.LogTransientWarning(
                    "Fetching a page of entities between row key '{Min}' and row key '{Max}'.",
                    rkMin,
                    rkMax);

                var select = ColumnNameToQueryColumnArray.GetOrAdd(
                    context.ClientRequestIdColumn,
                    x => new[] { StorageUtility.RowKey, x });

                Page<TableEntity> page;
                using (CreateScope(new RetryContext(context)))
                {
                    page = await context
                        .Client
                        .QueryAsync<TableEntity>(
                            filter: x => x.PartitionKey == pk && x.RowKey.CompareTo(rkMin) >= 0 && x.RowKey.CompareTo(rkMax) <= 0,
                            maxPerPage: StorageUtility.MaxTakeCount,
                            select)
                        .AsPages()
                        .FirstAsync();
                }

                // Capture all of the matched entities.
                var matchedCount = 0;
                foreach (var entity in page.Values)
                {
                    if (trackedRowKeys.Contains(entity.RowKey))
                    {
                        output.Add(entity.RowKey, entity);
                        matchedCount++;
                    }
                }

                // Any desired row keys in the returned range that are not found are deleted.
                var pageMin = page.Values.Select(x => x.RowKey).Min(StringComparer.Ordinal);
                var pageMax = page.Values.Select(x => x.RowKey).Max(StringComparer.Ordinal);
                var missedCount = 0;
                foreach (var entity in context.TrackedEntities)
                {
                    if (output.ContainsKey(entity.RowKey))
                    {
                        continue;
                    }

                    if (StringComparer.Ordinal.Compare(entity.RowKey, pageMin) > 0
                        && StringComparer.Ordinal.Compare(entity.RowKey, pageMax) < 0)
                    {
                        output.Add(entity.RowKey, null);
                        missedCount++;
                    }
                }

                _logger.LogTransientWarning(
                    "Fetched {Count} entities with row keys between '{Min}' and '{Max}', with {FetchedCount} matched and {MissedCount} missed.",
                    page.Values.Count,
                    pageMin,
                    pageMax,
                    matchedCount,
                    missedCount);
            }

            // Fetch the remaining using point reads.
            var fetchedCount = 0;
            foreach (var entity in context.TrackedEntities)
            {
                if (output.ContainsKey(entity.RowKey))
                {
                    continue;
                }

                var remote = await GetRemoteEntityOrNullAsync(context, pk, entity.RowKey);
                fetchedCount++;
                output.Add(entity.RowKey, remote?.Value);
            }

            _logger.LogTransientWarning("Fetched {Count} entities with point reads.", fetchedCount);

            var clientRequestIds = output
                .Values
                .Select(entity => GetClientRequestId(context, entity))
                .ToList();
            var found = output.Values.Count(x => x is not null);
            var notFound = output.Count - found;
            var matching = clientRequestIds.Count(x => context.ClientRequestId == x);

            _logger.LogTransientWarning(
                "For the batch transaction, {Found} entities were found, {NotFound} entities were not found, and {Matching} has matching client request IDs.",
                found,
                notFound,
                matching);

            var otherClientRequestIds = clientRequestIds.Where(x => x.HasValue && x != context.ClientRequestId).Distinct().Order().ToList();
            if (otherClientRequestIds.Count > 0)
            {
                _logger.LogTransientWarning("The non-matching client request IDs are: {OtherClientRequestIds}", otherClientRequestIds);
            }

            return output;
        }

        private StubResponse GetMultipartResponse(TableRetryBatchContext context, Dictionary<string, TableEntity?> remoteEntities)
        {
            // We have evidence that the batch went through. But we don't necessarily have all of the latest etags.
            // We'll fabricate a batch response body returning known matching etags and stale etags in all other cases.
            // This allows a best effort continuation but future updates using the stale etags will fail.

            // Ideally we'd use System.Net.Http.MultipartContent here but it adds double quotes around the boundary and
            // an extra CRLF before that last outer boundary. The make the Azure.Core multipart parser fail.

            var batchBoundary = $"batchresponse_{Guid.NewGuid()}";
            var changesetBoundary = $"changesetresponse_{Guid.NewGuid()}";

            var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, UTF8NoBom, leaveOpen: true))
            {
                writer.Write("--");
                writer.Write(batchBoundary);
                writer.Write("\r\n");
                writer.Write("Content-Type: multipart/mixed; boundary=");
                writer.Write(changesetBoundary);
                writer.Write("\r\n");
                writer.Write("\r\n");

                for (var i = 0; i < context.Actions.Count; i++)
                {
                    var action = context.Actions[i];

                    writer.Write("--");
                    writer.Write(changesetBoundary);
                    writer.Write("\r\n");
                    writer.Write("Content-Type: application/http\r\n");
                    writer.Write("Content-Transfer-Encoding: binary\r\n");
                    writer.Write("\r\n");
                    writer.Write("HTTP/1.1 204 No Content\r\n");
                    writer.Write("X-Content-Type-Options: nosniff\r\n");
                    writer.Write("Cache-Control: no-cache\r\n");
                    writer.Write("DataServiceVersion: 1.0;\r\n");

                    if (action.ActionType != TableTransactionActionType.Delete)
                    {
                        string etag;
                        if (remoteEntities.TryGetValue(action.Entity.RowKey, out var remote)
                            && remote is not null
                            && GetClientRequestId(context, remote) == context.ClientRequestId)
                        {
                            etag = remote.ETag.ToString("H");
                        }
                        else
                        {
                            _logger.LogWarning(
                                "The action at index {Index} for partition key '{PartitionKey}' and row key '{RowKey}' has been superceded. Using a stale etag for the multipart/mixed response.",
                                i,
                                action.Entity.PartitionKey,
                                action.Entity.RowKey);

                            etag = StaleETag;
                        }

                        writer.Write("ETag: ");
                        writer.Write(etag);
                        writer.Write("\r\n");
                    }

                    writer.Write("\r\n");
                    writer.Write("\r\n");
                }

                writer.Write("--");
                writer.Write(changesetBoundary);
                writer.Write("--");
                writer.Write("\r\n");
                writer.Write("--");
                writer.Write(batchBoundary);
                writer.Write("--");
                writer.Write("\r\n");
                writer.Flush();
            }

            memoryStream.Position = 0;

            var response = new StubResponse(
                (int)HttpStatusCode.Accepted,
                "Accepted",
                context.ClientRequestIdString,
                new Dictionary<string, string>
                {
                    { ClientRequestIdHeader, context.ClientRequestIdString },
                    { "Content-Type", $"multipart/mixed; boundary={batchBoundary}" },
                    { "Content-Length", memoryStream.Length.ToString(CultureInfo.InvariantCulture) },
                })
            {
                ContentStream = memoryStream,
            };

            return response;
        }

        private Guid? GetClientRequestId(TableRetryContext context, TableEntity? entity)
        {
            if (entity is not null
                && entity.TryGetValue(context.ClientRequestIdColumn, out var clientRequestIdObj)
                && clientRequestIdObj is Guid clientRequestId)
            {
                return clientRequestId;
            }

            return null;
        }
    }
}

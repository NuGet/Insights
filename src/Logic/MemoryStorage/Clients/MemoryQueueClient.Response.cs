// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Storage.Queues.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryQueueClient
    {
        private Response<bool> ExistsResponse()
        {
            if (_store.Exists())
            {
                return Response.FromValue(
                    true,
                    new MemoryResponse(HttpStatusCode.OK));
            }

            return Response.FromValue(
                false,
                new MemoryResponse(HttpStatusCode.NotFound));
        }

        private Response CreateIfNotExistsResponse(IDictionary<string, string>? metadata)
        {
            var result = _store.CreateIfNotExists(metadata);
            return result switch
            {
                StorageResultType.Success => new MemoryResponse(HttpStatusCode.Created),
                StorageResultType.AlreadyExists => new MemoryResponse(HttpStatusCode.NoContent),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }

        private Response<SendReceipt> SendMessageResponse(
            string messageText,
            TimeSpan? visibilityTimeout,
            TimeSpan? timeToLive)
        {
            var result = _store.SendMessage(messageText, visibilityTimeout, timeToLive);
            return result.Type switch
            {
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => Response.FromValue(
                    result.Value,
                    new MemoryResponse(HttpStatusCode.Created)),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }

        private Response<QueueMessage[]> ReceiveMessagesResponse(int? maxMessages, TimeSpan? visibilityTimeout)
        {
            var result = _store.ReceiveMessages(maxMessages, visibilityTimeout);
            return result.Type switch
            {
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => Response.FromValue(
                    result.Value,
                    new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }

        private Response<QueueMessage?> ReceiveMessageResponse(TimeSpan? visibilityTimeout)
        {
            var result = _store.ReceiveMessages(maxMessages: 1, visibilityTimeout);
            return result.Type switch
            {
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => Response.FromValue(
                    result.Value.FirstOrDefault(),
                    new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }

        private Response DeleteMessageResponse(string messageId, string popReceipt)
        {
            var result = _store.DeleteMessage(messageId, popReceipt);
            return result switch
            {
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => new MemoryResponse(HttpStatusCode.NoContent),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }

        private Response DeleteResponse()
        {
            var result = _store.Delete();
            return result switch
            {
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => new MemoryResponse(HttpStatusCode.NoContent),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }

        private Response<QueueProperties> GetPropertiesResponse()
        {
            var result = _store.GetProperties();
            return result.Type switch
            {
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => Response.FromValue(
                    result.Value,
                    new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }

        private Response<PeekedMessage[]> PeekMessagesResponse(int? maxMessages)
        {
            var result = _store.PeekMessages(maxMessages);
            return result.Type switch
            {
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => Response.FromValue(
                    result.Value,
                    new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }

        private Response<PeekedMessage?> PeekMessageResponse()
        {
            var result = _store.PeekMessages(maxMessages: 1);
            return result.Type switch
            {
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => Response.FromValue(
                    result.Value.FirstOrDefault(),
                    new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }

        private Response<UpdateReceipt> UpdateMessageResponse(
            string messageId,
            string popReceipt,
            string? messageText,
            TimeSpan visibilityTimeout)
        {
            var result = _store.UpdateMessage(messageId, popReceipt, messageText, visibilityTimeout);
            return result.Type switch
            {
                StorageResultType.DoesNotExist => throw new RequestFailedException(new MemoryResponse(HttpStatusCode.NotFound)),
                StorageResultType.Success => Response.FromValue(
                    result.Value,
                    new MemoryResponse(HttpStatusCode.OK)),
                _ => throw new NotImplementedException("Unexpected result type: " + result),
            };
        }
    }
}

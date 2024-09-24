// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Core;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public sealed class MemoryResponse : Response
    {
        private readonly ETag? _etag;

        public MemoryResponse(HttpStatusCode status)
            : this((int)status, GetDefaultReasonPhrase(status), $"client-request-id-{Guid.NewGuid()}", etag: null)
        {
        }

        public MemoryResponse(HttpStatusCode status, ETag? etag)
            : this((int)status, GetDefaultReasonPhrase(status), $"client-request-id-{Guid.NewGuid()}", etag)
        {
        }

        public MemoryResponse(int status, string reasonPhrase, string clientRequestId, ETag? etag)
        {
            Status = status;
            ReasonPhrase = reasonPhrase;
            ClientRequestId = clientRequestId;
            _etag = etag;
        }

        public override int Status { get; }
        public override string ReasonPhrase { get; }

        public override Stream? ContentStream { get; set; }
        public override string ClientRequestId { get; set; }

        public override void Dispose() => throw new NotImplementedException();
        protected override bool ContainsHeader(string name) => throw new NotImplementedException();
        protected override IEnumerable<HttpHeader> EnumerateHeaders() => throw new NotImplementedException();

        protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(name, "ETag")
                && _etag.HasValue)
            {
                value = _etag.Value.ToString();
                return true;
            }

            throw new NotImplementedException();
        }

        protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
        {
            throw new NotImplementedException();
        }

        private static readonly ConcurrentDictionary<HttpStatusCode, string> DefaultReasonPhrases = new();

        private static string GetDefaultReasonPhrase(HttpStatusCode status)
        {
            return DefaultReasonPhrases.GetOrAdd(status, x =>
            {
                using var response = new HttpResponseMessage(status);
                return response.ReasonPhrase!;
            });
        }
    }
}

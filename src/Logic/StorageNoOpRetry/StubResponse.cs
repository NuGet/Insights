// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Core;

#nullable enable

namespace NuGet.Insights.StorageNoOpRetry
{
    public class StubResponse : Response
    {
        private readonly IReadOnlyDictionary<string, string> _headers;

        public StubResponse(
            int status,
            string reasonPhrase,
            string clientRequestId,
            IReadOnlyDictionary<string, string> headers)
        {
            Status = status;
            ReasonPhrase = reasonPhrase;
            ClientRequestId = clientRequestId;
            _headers = headers;
        }

        public override int Status { get; }
        public override string ReasonPhrase { get; }

        public override Stream? ContentStream { get; set; }
        public override string ClientRequestId { get; set; }

        public override void Dispose()
        {
            ContentStream?.Dispose();
        }

        protected override bool ContainsHeader(string name)
        {
            return _headers.ContainsKey(name);
        }

        protected override IEnumerable<HttpHeader> EnumerateHeaders()
        {
            foreach ((var header, var value) in _headers)
            {
                yield return new HttpHeader(header, value);
            }
        }

        protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
        {
            if (_headers.TryGetValue(name, out value))
            {
                return true;
            }

            return false;
        }

        protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
        {
            if (_headers.TryGetValue(name, out var value))
            {
                values = new[] { value };
                return true;
            }

            values = null;
            return false;
        }
    }
}

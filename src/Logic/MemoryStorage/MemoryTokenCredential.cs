// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Core;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public class MemoryTokenCredential : TokenCredential
    {
        public static MemoryTokenCredential Instance { get; } = new();

        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new AccessToken($"access-token-{Guid.NewGuid()}", DateTimeOffset.UtcNow.AddMinutes(15)));
        }
    }
}

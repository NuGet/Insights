// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Core;

#nullable enable

namespace NuGet.Insights
{
    public class CachingTokenCredential : TokenCredential
    {
        private readonly AccessTokenCache _cache;
        private readonly TokenCredential _credential;
        private readonly ILogger<CachingTokenCredential> _logger;

        public static TokenCredential MaybeWrap(TokenCredential credential, ILoggerFactory loggerFactory, StorageSettings settings)
        {
            if (!settings.UseAccessTokenCaching)
            {
                return credential;
            }

            return new CachingTokenCredential(
                AccessTokenCache.Shared,
                credential,
                loggerFactory.CreateLogger<CachingTokenCredential>());
        }

        public CachingTokenCredential(AccessTokenCache cache, TokenCredential credential, ILogger<CachingTokenCredential> logger)
        {
            _cache = cache;
            _credential = credential;
            _logger = logger;
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return _cache.GetToken(requestContext, _credential, _logger, cancellationToken);
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return await _cache.GetTokenAsync(requestContext, _credential, _logger, cancellationToken);
        }
    }
}

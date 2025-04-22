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
        private readonly string? _defaultTenantId;
        private readonly ILogger<CachingTokenCredential> _logger;

        public static TokenCredential MaybeWrap(TokenCredential credential, ILoggerFactory loggerFactory, StorageSettings settings, string? defaultTenantId)
        {
            if (!settings.UseAccessTokenCaching)
            {
                if (!string.IsNullOrWhiteSpace(defaultTenantId))
                {
                    throw new ArgumentException($"The tenant ID override is not supported when {nameof(settings.UseAccessTokenCaching)} is false.", nameof(defaultTenantId));
                }

                return credential;
            }

            return new CachingTokenCredential(
                AccessTokenCache.Shared,
                credential,
                defaultTenantId,
                loggerFactory.CreateLogger<CachingTokenCredential>());
        }

        public CachingTokenCredential(AccessTokenCache cache, TokenCredential credential, string? defaultTenantId, ILogger<CachingTokenCredential> logger)
        {
            _cache = cache;
            _credential = credential;
            _defaultTenantId = defaultTenantId;
            _logger = logger;
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            requestContext = SetDefaultTenantId(requestContext);
            return _cache.GetToken(requestContext, _credential, _logger, cancellationToken);
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            requestContext = SetDefaultTenantId(requestContext);
            return await _cache.GetTokenAsync(requestContext, _credential, _logger, cancellationToken);
        }

        private TokenRequestContext SetDefaultTenantId(TokenRequestContext requestContext)
        {
            if (requestContext.TenantId is null && _defaultTenantId is not null)
            {
                // The default tenant ID is only used when the request context does not have a tenant ID.
                // This is to avoid breaking changes for existing code that uses this credential.
                requestContext = new TokenRequestContext(
                    requestContext.Scopes,
                    requestContext.ParentRequestId,
                    requestContext.Claims,
                    _defaultTenantId,
                    requestContext.IsCaeEnabled,
                    requestContext.IsProofOfPossessionEnabled,
                    requestContext.ProofOfPossessionNonce,
                    requestContext.ResourceRequestUri,
                    requestContext.ResourceRequestMethod);
            }

            return requestContext;
        }
    }
}

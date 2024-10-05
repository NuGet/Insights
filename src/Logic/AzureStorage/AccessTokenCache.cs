// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Core;
using Azure.Core.Pipeline;
using ScopedAccessTokenCache = Azure.Core.Pipeline.Fork.BearerTokenAuthenticationPolicy.AccessTokenCache;

#nullable enable

namespace NuGet.Insights
{
    public class AccessTokenCache
    {
        public static AccessTokenCache Shared { get; } = new();

        private readonly ConcurrentDictionary<AccessTokenCacheKey, ScopedAccessTokenCache> _cache = new();

        public AccessToken GetToken(
            TokenRequestContext requestContext,
            TokenCredential credential,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            return GetCache(requestContext, credential, logger)
                .GetAuthHeaderValueAsync(cancellationToken, requestContext, async: false)
                .EnsureCompleted();
        }

        public async ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            TokenCredential credential,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            return await GetCache(requestContext, credential, logger)
                .GetAuthHeaderValueAsync(cancellationToken, requestContext, async: true)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        private ScopedAccessTokenCache GetCache(TokenRequestContext requestContext, TokenCredential credential, ILogger logger)
        {
            var key = new AccessTokenCacheKey(requestContext, credential, logger);
            return _cache.GetOrAdd(key, static k =>
            {
                k.CopyScopes();
                return new ScopedAccessTokenCache(k.Credential, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30), k.Logger);
            });
        }

        private class AccessTokenCacheKey : IEquatable<AccessTokenCacheKey>
        {
            private readonly int _hashCode;

            public AccessTokenCacheKey(TokenRequestContext requestContext, TokenCredential credential, ILogger logger)
            {
                if (requestContext.Claims != null)
                {
                    throw new NotImplementedException();
                }

                if (requestContext.TenantId != null)
                {
                    throw new NotImplementedException();
                }

                if (requestContext.IsCaeEnabled)
                {
                    throw new NotImplementedException();
                }

                Scopes = requestContext.Scopes;
                Credential = credential;
                Logger = logger;

                var hashCode = new HashCode();
                foreach (var scope in Scopes)
                {
                    hashCode.Add(scope);
                }

                _hashCode = hashCode.ToHashCode();
            }

            internal void CopyScopes()
            {
                Scopes = Scopes.ToArray();
            }

            public IReadOnlyList<string> Scopes { get; private set; }
            public TokenCredential Credential { get; }
            public ILogger Logger { get; }

            public override bool Equals(object? obj)
            {
                return Equals(obj as AccessTokenCacheKey);
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }

            public bool Equals(AccessTokenCacheKey? other)
            {
                if (other is null)
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                if (Scopes.Count != other.Scopes.Count)
                {
                    return false;
                }

                for (var i = 0; i < Scopes.Count; i++)
                {
                    if (Scopes[i] != other.Scopes[i])
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}

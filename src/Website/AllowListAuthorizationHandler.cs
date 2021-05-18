// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Identity.Web;

namespace NuGet.Insights.Website
{
    public class AllowListAuthorizationHandler : AuthorizationHandler<AllowListRequirement>
    {
        public const string PolicyName = "AllowList";
        private const string AllowedGroupClaimName = "NuGet.Insights.AllowedGroup";
        private const string HttpContextKeyForJwt = "JwtSecurityTokenUsedToCallWebAPI";

        private readonly IServiceProvider _serviceProvider;
        private readonly bool _restrictUsers;
        private readonly Dictionary<string, HashSet<string>> _allowedUsers;
        private readonly Dictionary<string, HashSet<string>> _allowedGroups;

        public AllowListAuthorizationHandler(
            IServiceProvider serviceProvider,
            IOptions<NuGetInsightsWebsiteSettings> options)
        {
            _serviceProvider = serviceProvider;
            _restrictUsers = options.Value.RestrictUsers;
            _allowedUsers = TenantToObjectIds(options.Value.AllowedUsers);
            _allowedGroups = TenantToObjectIds(options.Value.AllowedGroups);
        }

        private static Dictionary<string, HashSet<string>> TenantToObjectIds(IEnumerable<AllowedObject> objects)
        {
            if (objects == null)
            {
                return new Dictionary<string, HashSet<string>>();
            }

            return objects
                .GroupBy(x => x.TenantId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.ObjectId).ToHashSet());
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AllowListRequirement requirement)
        {
            if (!_restrictUsers
                || IsUserAllowed(context)
                || IsUserInAllowedGroup(context))
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }

            return Task.CompletedTask;
        }

        private bool IsUserAllowed(AuthorizationHandlerContext context)
        {
            if (!TryGetObjectIds(context.User, _allowedUsers, out var objectIds))
            {
                return false;
            }

            var objectId = context.User.FindFirst(ClaimConstants.ObjectId);
            if (objectId == null
                || string.IsNullOrWhiteSpace(objectId.Value)
                || !objectIds.Contains(objectId.Value))
            {
                return false;
            }

            return true;
        }

        private bool IsUserInAllowedGroup(AuthorizationHandlerContext context)
        {
            if (!TryGetObjectIds(context.User, _allowedGroups, out var objectIds))
            {
                return false;
            }

            var claims = context.User.FindAll(AllowedGroupClaimName);
            foreach (var claim in claims)
            {
                if (!string.IsNullOrWhiteSpace(claim.Value)
                    && objectIds.Contains(claim.Value))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task AddAllowedGroupClaimsAsync(TokenValidatedContext context)
        {
            if (!TryGetObjectIds(context.Principal, _allowedGroups, out var objectIds))
            {
                return;
            }

            try
            {
                // Source: https://github.com/Azure-Samples/active-directory-aspnetcore-webapp-openidconnect-v2/blob/ef20861535add11f5d37e25228379c8dfc5d1796/5-WebApp-AuthZ/5-2-Groups/Services/MicrosoftGraph-Rest/GraphHelper.cs
                context.HttpContext.Items[HttpContextKeyForJwt] = context.SecurityToken;

                var memberGroups = await _serviceProvider
                    .GetRequiredService<GraphServiceClient>()
                    .Me
                    .CheckMemberGroups(objectIds)
                    .Request()
                    .PostAsync();

                var allowedGroups = memberGroups.ToHashSet();
                var identity = (ClaimsIdentity)context.Principal.Identity;
                foreach (var allowedGroup in allowedGroups)
                {
                    var claim = new Claim(AllowedGroupClaimName, allowedGroup);
                    identity.AddClaim(claim);
                }
            }
            finally
            {
                context.HttpContext.Items.Remove(HttpContextKeyForJwt);
            }
        }

        private static bool TryGetObjectIds(
            ClaimsPrincipal user,
            Dictionary<string, HashSet<string>> tenantToObjectIds,
            out HashSet<string> objectIds)
        {
            if (!user.Identity.IsAuthenticated)
            {
                objectIds = null;
                return false;
            }

            var tenantId = user.FindFirst(ClaimConstants.TenantId);
            if (tenantId == null
                || string.IsNullOrWhiteSpace(tenantId.Value)
                || !tenantToObjectIds.TryGetValue(tenantId.Value, out objectIds)
                || !objectIds.Any())
            {
                objectIds = null;
                return false;
            }

            return true;
        }

        private class SimpleAuthenticationProvider : IAuthenticationProvider
        {
            private readonly string _token;

            public SimpleAuthenticationProvider(string token)
            {
                _token = token;
            }

            public Task AuthenticateRequestAsync(HttpRequestMessage request)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    Microsoft.Identity.Web.Constants.Bearer,
                    _token);
                return Task.CompletedTask;
            }
        }
    }
}

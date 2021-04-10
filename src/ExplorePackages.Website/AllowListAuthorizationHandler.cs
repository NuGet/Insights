using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace Knapcode.ExplorePackages.Website
{
    public class AllowListAuthorizationHandler : AuthorizationHandler<AllowListRequirement>
    {
        public const string PolicyName = "AllowList";
        private readonly bool _restrictUsers;
        private readonly Dictionary<string, HashSet<string>> _allowedUsers;

        public AllowListAuthorizationHandler(IOptions<ExplorePackagesWebsiteSettings> options)
        {
            _restrictUsers = options.Value.RestrictUsers;
            _allowedUsers = options
                .Value
                .AllowedUsers
                .GroupBy(x => x.TenantId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.ObjectId).ToHashSet());
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AllowListRequirement requirement)
        {
            if (!_restrictUsers)
            {
                context.Succeed(requirement);
            }
            else
            {
                var tenantId = context.User.FindFirst(ClaimConstants.TenantId);
                if (tenantId == null
                    || string.IsNullOrWhiteSpace(tenantId.Value)
                    || !_allowedUsers.TryGetValue(tenantId.Value, out var objectIds))
                {
                    context.Fail();
                }
                else
                {
                    var objectId = context.User.FindFirst(ClaimConstants.ObjectId);
                    if (objectId == null
                        || string.IsNullOrWhiteSpace(objectId.Value)
                        || !objectIds.Contains(objectId.Value))
                    {
                        context.Fail();
                    }
                    else
                    {
                        context.Succeed(requirement);
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}

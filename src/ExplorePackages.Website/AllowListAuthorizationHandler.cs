using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace Knapcode.ExplorePackages.Website
{
    public class AllowListAuthorizationHandler : AuthorizationHandler<AllowListRequirement>
    {
        public const string PolicyName = "AllowList";
        private readonly Dictionary<string, HashSet<string>> _allowedUsers;

        public AllowListAuthorizationHandler(IOptions<ExplorePackagesWebsiteSettings> options)
        {
            _allowedUsers = options
                .Value
                .AllowedUsers
                .GroupBy(x => x.TenantId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.NameIdentifier).ToHashSet());
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AllowListRequirement requirement)
        {
            var tenantId = context.User.FindFirst(ClaimConstants.TenantId);
            if (tenantId == null
                || !_allowedUsers.TryGetValue(tenantId.Value, out var nameIdentifiers))
            {
                context.Fail();
                return Task.CompletedTask;
            }
            else
            {
                var nameIdentifer = context.User.FindFirst(ClaimTypes.NameIdentifier);
                if (!nameIdentifiers.Contains(nameIdentifer.Value))
                {
                    context.Fail();
                }
                else
                {
                    context.Succeed(requirement);
                }
            }

            return Task.CompletedTask;
        }
    }
}

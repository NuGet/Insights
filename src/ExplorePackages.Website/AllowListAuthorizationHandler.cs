using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
                .GroupBy(x => x.HashedTenantId)
                .ToDictionary(x => x.Key, x => x.Select(y => y.HashedObjectId).ToHashSet());
        }

        public static string HashValue(string input)
        {
            const string salt = "Knapcode.ExplorePackages-8DHU4R9URVLNHTQC2SS21ATB95U1VD1J-";
            using (var algorithm = SHA256.Create())
            {
                var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(salt + input));
                return bytes.ToHex();
            }
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AllowListRequirement requirement)
        {
            var tenantId = context.User.FindFirst(ClaimConstants.TenantId);
            if (tenantId == null
                || !_allowedUsers.TryGetValue(HashValue(tenantId.Value), out var hashedObjectIds))
            {
                context.Fail();
            }
            else
            {
                var objectId = context.User.FindFirst(ClaimConstants.ObjectId);
                if (objectId == null
                    || !hashedObjectIds.Contains(HashValue(objectId.Value)))
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

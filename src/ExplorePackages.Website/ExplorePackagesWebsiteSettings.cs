using System.Collections.Generic;
using Knapcode.ExplorePackages.Worker;

namespace Knapcode.ExplorePackages.Website
{
    public class ExplorePackagesWebsiteSettings : ExplorePackagesWorkerSettings
    {
        public List<AllowedUser> AllowedUsers { get; set; } = new List<AllowedUser>();
    }
}

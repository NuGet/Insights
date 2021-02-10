using System.Collections.Generic;
using Knapcode.ExplorePackages.Worker;

namespace Knapcode.ExplorePackages.Website
{
    public class ExplorePackagesWebsiteSettings : ExplorePackagesWorkerSettings
    {
        public bool ShowAdminLink { get; set; } = true;
        public bool RestrictUsers { get; set; } = true;
        public List<AllowedUser> AllowedUsers { get; set; } = new List<AllowedUser>();
    }
}

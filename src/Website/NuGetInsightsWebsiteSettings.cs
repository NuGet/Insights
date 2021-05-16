using System.Collections.Generic;
using NuGet.Insights.Worker;

namespace NuGet.Insights.Website
{
    public class NuGetInsightsWebsiteSettings : NuGetInsightsWorkerSettings
    {
        public bool ShowAdminLink { get; set; } = true;
        public bool RestrictUsers { get; set; } = true;
        public List<AllowedObject> AllowedUsers { get; set; } = new List<AllowedObject>();
        public List<AllowedObject> AllowedGroups { get; set; } = new List<AllowedObject>();
    }
}

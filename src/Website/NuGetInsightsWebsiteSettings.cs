// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker;

namespace NuGet.Insights.Website
{
    public class NuGetInsightsWebsiteSettings : NuGetInsightsWorkerSettings
    {
        /// <summary>
        /// Whether or not to show the admin link on the home page.
        /// </summary>
        public bool ShowAdminLink { get; set; } = true;

        public bool RestrictUsers { get; set; } = true;
        public List<AllowedObject> AllowedUsers { get; set; } = new List<AllowedObject>();
        public List<AllowedObject> AllowedGroups { get; set; } = new List<AllowedObject>();

        /// <summary>
        /// Whether or not to show some non-sensitive workflow run and catalog scan metadata on the home page.
        /// </summary>
        public bool ShowAdminMetadata { get; set; } = true;

        public TimeSpan CachedAdminViewModelMaxAge { get; set; } = TimeSpan.FromMinutes(5);
    }
}

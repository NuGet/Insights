// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class IgnoredPackagePattern
    {
        public string IdRegex { get; set; }

        /// <summary>
        /// Inclusive start date for the pattern.
        /// If the package ID matches this regex and the package commit timestamp is after or equal to this time, then the package is ignored.
        /// </summary>
        public DateTimeOffset MinTimestamp { get; set; } = DateTimeOffset.MinValue;

        /// <summary>
        /// Inclusive end date for the pattern.
        /// If the package ID matches this regex and the package commit timestamp is before or equal to this time, then the package is ignored.
        /// </summary>
        public DateTimeOffset MaxTimestamp { get; set; } = DateTimeOffset.MaxValue;
    }
}

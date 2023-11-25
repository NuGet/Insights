// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class V2Package
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? LastEdited { get; set; }
        public DateTimeOffset Published { get; set; }
        public DateTimeOffset LastUpdated { get; set; }

        public bool Listed => Published.Year != 1900;
    }
}

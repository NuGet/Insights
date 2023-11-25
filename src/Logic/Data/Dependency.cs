// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class Dependency
    {
        public Dependency(string id, string version, VersionRange parsedVersionRange)
        {
            Id = id;
            Version = version;
            ParsedVersionRange = parsedVersionRange;
        }

        public string Id { get; }
        public string Version { get; }
        public VersionRange ParsedVersionRange { get; }
    }
}

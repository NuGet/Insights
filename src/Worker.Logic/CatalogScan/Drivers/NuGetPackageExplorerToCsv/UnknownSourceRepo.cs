// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    public record UnknownSourceRepo : SourceUrlRepo
    {
        public override string Type => "Unknown";
        public string Host { get; init; }
    }
}

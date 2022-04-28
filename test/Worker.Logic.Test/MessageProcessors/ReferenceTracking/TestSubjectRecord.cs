// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public partial class TestSubjectRecord : ICsvRecord
    {
        [KustoPartitionKey]
        public string BucketKey { get; set; }

        public string Id { get; set; }

        public bool IsOrphan { get; set; }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoBlobIngestionMessage
    {
        public string StorageSuffix { get; set; }
        public string ContainerName { get; set; }
        public string BlobName { get; set; }
        public int AttemptCount { get; set; }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights.Worker
{
    public record CsvRecordStorage : ICsvRecordStorage
    {
        public CsvRecordStorage(string containerName, Type recordType, string blobNamePrefix)
        {
            ContainerName = containerName;
            RecordType = recordType;
            BlobNamePrefix = blobNamePrefix;
        }

        public string ContainerName { get; }
        public Type RecordType { get; }
        public string BlobNamePrefix { get; }
    }
}

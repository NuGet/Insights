// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class CsvRecordBlob
    {
        public CsvRecordBlob(string containerName, string name, long compressedSizeBytes, long rawSizeBytes, long recordCount)
        {
            ContainerName = containerName;
            Name = name;
            CompressedSizeBytes = compressedSizeBytes;
            RawSizeBytes = rawSizeBytes;
            RecordCount = recordCount;
        }

        public string ContainerName { get; }
        public string Name { get; }
        public long CompressedSizeBytes { get; }
        public long RawSizeBytes { get; }
        public long RecordCount { get; }
    }
}

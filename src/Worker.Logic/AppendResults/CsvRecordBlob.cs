// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Blobs.Models;

namespace NuGet.Insights.Worker
{
    public class CsvRecordBlob
    {
        public CsvRecordBlob(
            string containerName,
            string name,
            BlobProperties properties) : this(
                containerName,
                name,
                properties.ContentLength,
                properties.Metadata)
        {
        }

        public CsvRecordBlob(
            string containerName,
            BlobItem item) : this(
                containerName,
                item.Name,
                item.Properties.ContentLength.Value,
                item.Metadata)
        {
        }

        public CsvRecordBlob(
            string containerName,
            string name,
            long compressedSizeBytes,
            IDictionary<string, string> metadata) : this(
                containerName,
                name,
                compressedSizeBytes,
                GetLongOrNull(metadata, StorageUtility.RawSizeBytesMetadata),
                GetLongOrNull(metadata, StorageUtility.RecordCountMetadata))
        {
        }

        private static long? GetLongOrNull(IDictionary<string, string> metadata, string key)
        {
            if (metadata.TryGetValue(key, out var unparsedValue)
                && long.TryParse(unparsedValue, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            return null;
        }

        public CsvRecordBlob(
            string containerName,
            string name,
            long compressedSizeBytes,
            long? rawSizeBytes,
            long? recordCount)
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
        public long? RawSizeBytes { get; }
        public long? RecordCount { get; }
    }
}

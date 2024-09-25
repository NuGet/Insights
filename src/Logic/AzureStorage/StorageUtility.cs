// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Buffers;
using System.Security.Cryptography;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.WebUtilities;

#nullable enable

namespace NuGet.Insights
{
    public class StorageUtility
    {
        public const int MaxBatchSize = 100;
        public const int MaxTakeCount = 1000;
        public const int MaxDequeueCount = 32;

        public const string PartitionKey = "PartitionKey";
        public const string RowKey = "RowKey";
        public const string Timestamp = "Timestamp";
        public const string ETag = "odata.etag";

        public const string MD5Header = "Content-MD5";
        public const string SHA512Header = "x-ms-meta-SHA512";

        public const string MemoryStorageAccountName = "memory";
        public const string DevelopmentConnectionString = "UseDevelopmentStorage=true";

        /// <summary>
        /// The minimum value for a timetamp in Azure Table Storage.
        /// Source: https://learn.microsoft.com/en-us/rest/api/storageservices/Understanding-the-Table-Service-Data-Model#property-types
        /// </summary>
        public static readonly DateTimeOffset MinTimestamp = new DateTimeOffset(1601, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// See: https://docs.microsoft.com/en-us/azure/data-explorer/lightingest#recommendations
        /// </summary>
        public const string RawSizeBytesMetadata = "rawSizeBytes";

        public const string RecordCountMetadata = "recordCount";

        public static readonly IList<string> MinSelectColumns = [PartitionKey, RowKey];

        public static int GetBucket(int bucketCount, string bucketKey)
        {
            return GetBucket(bucketCount, bucketKey, ReadOnlySpan<byte>.Empty);
        }

        public static int GetBucket(int bucketCount, string bucketKey, ReadOnlySpan<byte> suffix)
        {
            if (!BitConverter.IsLittleEndian)
            {
                throw new NotSupportedException($"The {nameof(GetBucket)} method only works on little endian systems right now.");
            }

            int bucket;

            var maxBytes = bucketKey.Length * 4 + suffix.Length; // UTF-8 worst case
            const int maxStackBytes = 256;
            var usePool = maxBytes > maxStackBytes;
            var pool = ArrayPool<byte>.Shared;
            byte[]? poolBuffer = usePool ? pool.Rent(maxBytes) : null;
            Span<byte> buffer = usePool ? poolBuffer : stackalloc byte[maxStackBytes];
            try
            {
                var bytesWritten = Encoding.UTF8.GetBytes(bucketKey, buffer);
                if (suffix.Length > 0)
                {
                    suffix.CopyTo(buffer.Slice(bytesWritten, suffix.Length));
                }

                Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
                if (!SHA256.TryHashData(buffer.Slice(0, bytesWritten + suffix.Length), hash, out var hashBytesWritten))
                {
                    throw new InvalidOperationException("Could not compute the bucket.");
                }

                bucket = (int)(BitConverter.ToUInt64(hash) % (ulong)bucketCount);
            }
            finally
            {
                if (usePool)
                {
                    pool.Return(poolBuffer!);
                }
            }

            return bucket;
        }

        public static Uri DevelopmentBlobEndpoint { get; } = new BlobServiceClient(DevelopmentConnectionString).Uri;
        public static Uri DevelopmentQueueEndpoint { get; } = new QueueServiceClient(DevelopmentConnectionString).Uri;
        public static Uri DevelopmentTableEndpoint { get; } = new TableServiceClient(DevelopmentConnectionString).Uri;

        public static Uri GetBlobEndpoint(string accountName)
        {
            return new Uri($"https://{accountName}.blob.core.windows.net");
        }

        public static Uri GetQueueEndpoint(string accountName)
        {
            return new Uri($"https://{accountName}.queue.core.windows.net");
        }

        public static Uri GetTableEndpoint(string accountName)
        {
            return new Uri($"https://{accountName}.table.core.windows.net");
        }

        public static bool TryGetAccountBlobClient(
            BlobServiceClient serviceClient,
            Uri blobUri,
            [NotNullWhen(true)] out BlobClient? blobClient)
        {
            var uriBuilder = new BlobUriBuilder(blobUri);
            if (uriBuilder.AccountName != serviceClient.AccountName
                || string.IsNullOrEmpty(uriBuilder.BlobContainerName)
                || string.IsNullOrEmpty(uriBuilder.BlobName))
            {
                blobClient = null;
                return false;
            }

            blobClient = serviceClient
                .GetBlobContainerClient(uriBuilder.BlobContainerName)
                .GetBlobClient(uriBuilder.BlobName);

            if (blobClient.Uri != blobUri)
            {
                blobClient = null;
                return false;
            }

            return true;
        }

        public static string GenerateUniqueId()
        {
            return Guid.NewGuid().ToByteArray().ToTrimmedBase32();
        }

        public static StorageId GenerateDescendingId()
        {
            var descendingComponent = GetDescendingId(DateTimeOffset.UtcNow);
            var uniqueComponent = GenerateUniqueId();
            return new StorageId(descendingComponent, uniqueComponent);
        }

        public static string GetDescendingId(DateTimeOffset timestamp)
        {
            return (long.MaxValue - timestamp.Ticks).ToString("D20", CultureInfo.InvariantCulture);
        }

        public static TimeSpan GetMessageDelay(int attemptCount)
        {
            return GetMessageDelay(attemptCount, factor: 1);
        }

        public static TimeSpan GetMessageDelay(int attemptCount, int factor)
        {
            return GetMessageDelay(attemptCount, factor, maxSeconds: 60);
        }

        public static TimeSpan GetMessageDelay(int attemptCount, int factor, int maxSeconds)
        {
            return TimeSpan.FromSeconds(Math.Min(Math.Max(attemptCount * factor, 0), maxSeconds));
        }

        public static DateTimeOffset GetSasExpiry(string sas)
        {
            DateTimeOffset sasExpiry;
            var parsedSas = QueryHelpers.ParseQuery(sas);
            var expiry = parsedSas["se"].Single();
            sasExpiry = DateTimeOffset.Parse(expiry!, CultureInfo.InvariantCulture);
            return sasExpiry;
        }
    }
}

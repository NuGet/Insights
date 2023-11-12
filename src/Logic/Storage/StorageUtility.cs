// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

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

        /// <summary>
        /// The minimum value for a timetamp in Azure Table Storage.
        /// Source: https://learn.microsoft.com/en-us/rest/api/storageservices/Understanding-the-Table-Service-Data-Model#property-types
        /// </summary>
        public static readonly DateTimeOffset MinTimestamp = new DateTimeOffset(1601, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// See: https://docs.microsoft.com/en-us/azure/data-explorer/lightingest#recommendations
        /// </summary>
        public const string RawSizeBytesMetadata = "rawSizeBytes";

        public const string EmulatorConnectionString = "UseDevelopmentStorage=true";

        public static readonly IList<string> MinSelectColumns = new[] { PartitionKey, RowKey };

        public static int GetBucket(int bucketCount, string bucketKey)
        {
            if (!BitConverter.IsLittleEndian)
            {
                throw new NotSupportedException($"The {nameof(GetBucket)} method only works on little endian systems right now.");
            }

            int bucket;
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(bucketKey));
                bucket = (int)(BitConverter.ToUInt64(hash) % (ulong)bucketCount);
            }

            return bucket;
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
            return (long.MaxValue - timestamp.Ticks).ToString("D20");
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
            sasExpiry = DateTimeOffset.Parse(expiry);
            return sasExpiry;
        }
    }
}

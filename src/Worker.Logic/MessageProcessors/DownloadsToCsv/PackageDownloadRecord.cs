// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using MessagePack;
using MessagePack.Formatters;

namespace NuGet.Insights.Worker.DownloadsToCsv
{
    public partial record PackageDownloadRecord : IPackageDownloadRecord<PackageDownloadRecord>
    {
        [KustoIgnore]
        public DateTimeOffset AsOfTimestamp { get; set; }

        public string LowerId { get; set; }

        [BucketKey]
        [KustoPartitionKey]
        public string Identity { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }

        [Required]
        public long Downloads { get; set; }

        [Required]
        public long TotalDownloads { get; set; }

        public static IEqualityComparer<PackageDownloadRecord> KeyComparer => PackageDownloadRecordKeyComparer.Instance;
        public static IReadOnlyList<string> KeyFields { get; } = [nameof(Identity)];

        public int CompareTo(PackageDownloadRecord other)
        {
            return string.CompareOrdinal(Identity, other.Identity);
        }

        public static List<PackageDownloadRecord> Prune(
            List<PackageDownloadRecord> records,
            bool isFinalPrune,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger logger)
        {
            // some duplicate records exist in the source data, prefer the one with the highest download count
            return records
                .GroupBy(x => x.Identity)
                .Select(g => g.MaxBy(x => x.Downloads))
                .Order()
                .ToList();
        }

        public class PackageDownloadRecordKeyComparer : IEqualityComparer<PackageDownloadRecord>
        {
            public static PackageDownloadRecordKeyComparer Instance { get; } = new PackageDownloadRecordKeyComparer();

            public bool Equals(PackageDownloadRecord x, PackageDownloadRecord y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.Identity == y.Identity;
            }

            public int GetHashCode([DisallowNull] PackageDownloadRecord obj)
            {
                return obj.Identity.GetHashCode(StringComparison.Ordinal);
            }
        }

        public class PackageDownloadRecordMessagePackFormatter : IMessagePackFormatter<PackageDownloadRecord>
        {
            public static PackageDownloadRecordMessagePackFormatter Instance { get; } = new PackageDownloadRecordMessagePackFormatter();

            public void Serialize(ref MessagePackWriter writer, PackageDownloadRecord value, MessagePackSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNil();
                    return;
                }

                writer.WriteArrayHeader(7);
                MessagePackSerializer.Serialize(ref writer, value.AsOfTimestamp, options);
                writer.Write(value.LowerId);
                writer.Write(value.Identity);
                writer.Write(value.Id);
                writer.Write(value.Version);
                writer.Write(value.Downloads);
                writer.Write(value.TotalDownloads);
            }

            public PackageDownloadRecord Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                var record = new PackageDownloadRecord();
                var count = reader.ReadArrayHeader();

                if (count != 7)
                {
                    throw new MessagePackSerializationException($"Invalid array length: {count}");
                }

                record.AsOfTimestamp = MessagePackSerializer.Deserialize<DateTimeOffset>(ref reader, options);
                record.LowerId = reader.ReadString();
                record.Identity = reader.ReadString();
                record.Id = reader.ReadString();
                record.Version = reader.ReadString();
                record.Downloads = reader.ReadInt64();
                record.TotalDownloads = reader.ReadInt64();

                return record;
            }
        }
    }
}

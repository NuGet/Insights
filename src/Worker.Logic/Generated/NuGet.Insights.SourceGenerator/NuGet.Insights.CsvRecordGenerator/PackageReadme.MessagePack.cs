﻿// <auto-generated />

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;
using MessagePack.Formatters;
using NuGet.Insights;

namespace NuGet.Insights.Worker.PackageReadmeToCsv
{
    partial record PackageReadme
    {
        public class PackageReadmeMessagePackFormatter : IMessagePackFormatter<PackageReadme>
        {
            public void Serialize(ref MessagePackWriter writer, PackageReadme value, MessagePackSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNil();
                    return;
                }

                writer.WriteArrayHeader(13);

                MessagePackSerializer.Serialize(ref writer, value.ScanId, options);
                MessagePackSerializer.Serialize(ref writer, value.ScanTimestamp, options);
                writer.Write(value.LowerId);
                writer.Write(value.Identity);
                writer.Write(value.Id);
                writer.Write(value.Version);
                MessagePackSerializer.Serialize(ref writer, value.CatalogCommitTimestamp, options);
                MessagePackSerializer.Serialize(ref writer, value.Created, options);
                writer.Write((int)value.ResultType);
                MessagePackSerializer.Serialize(ref writer, value.Size, options);
                MessagePackSerializer.Serialize(ref writer, value.LastModified, options);
                writer.Write(value.SHA256);
                writer.Write(value.Content);
            }

            public PackageReadme Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                var count = reader.ReadArrayHeader();
                if (count != 13)
                {
                    throw new MessagePackSerializationException($"Invalid array length: {count}");
                }

                var record = new PackageReadme();

                record.ScanId = MessagePackSerializer.Deserialize<System.Guid?>(ref reader, options);
                record.ScanTimestamp = MessagePackSerializer.Deserialize<System.DateTimeOffset?>(ref reader, options);
                record.LowerId = reader.ReadString();
                record.Identity = reader.ReadString();
                record.Id = reader.ReadString();
                record.Version = reader.ReadString();
                record.CatalogCommitTimestamp = MessagePackSerializer.Deserialize<System.DateTimeOffset>(ref reader, options);
                record.Created = MessagePackSerializer.Deserialize<System.DateTimeOffset?>(ref reader, options);
                record.ResultType = (NuGet.Insights.Worker.PackageReadmeToCsv.PackageReadmeResultType)reader.ReadInt32();
                record.Size = MessagePackSerializer.Deserialize<int?>(ref reader, options);
                record.LastModified = MessagePackSerializer.Deserialize<System.DateTimeOffset?>(ref reader, options);
                record.SHA256 = reader.ReadString();
                record.Content = reader.ReadString();

                return record;
            }
        }
    }
}
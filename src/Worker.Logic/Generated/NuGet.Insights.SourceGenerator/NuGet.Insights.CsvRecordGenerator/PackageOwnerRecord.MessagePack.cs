﻿// <auto-generated />

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;
using MessagePack.Formatters;
using NuGet.Insights;

namespace NuGet.Insights.Worker.OwnersToCsv
{
    partial record PackageOwnerRecord
    {
        public class PackageOwnerRecordMessagePackFormatter : IMessagePackFormatter<PackageOwnerRecord>
        {
            public void Serialize(ref MessagePackWriter writer, PackageOwnerRecord value, MessagePackSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNil();
                    return;
                }

                writer.WriteArrayHeader(4);

                MessagePackSerializer.Serialize(ref writer, value.AsOfTimestamp, options);
                writer.Write(value.LowerId);
                writer.Write(value.Id);
                writer.Write(value.Owners);
            }

            public PackageOwnerRecord Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                var count = reader.ReadArrayHeader();
                if (count != 4)
                {
                    throw new MessagePackSerializationException($"Invalid array length: {count}");
                }

                var record = new PackageOwnerRecord();

                record.AsOfTimestamp = MessagePackSerializer.Deserialize<System.DateTimeOffset>(ref reader, options);
                record.LowerId = reader.ReadString();
                record.Id = reader.ReadString();
                record.Owners = reader.ReadString();

                return record;
            }
        }
    }
}
﻿// <auto-generated />

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;
using MessagePack.Formatters;
using NuGet.Insights;

namespace NuGet.Insights.Worker.PackageAssemblyToCsv
{
    partial record PackageAssembly
    {
        public class PackageAssemblyMessagePackFormatter : IMessagePackFormatter<PackageAssembly>
        {
            public void Serialize(ref MessagePackWriter writer, PackageAssembly value, MessagePackSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNil();
                    return;
                }

                writer.WriteArrayHeader(28);

                MessagePackSerializer.Serialize(ref writer, value.ScanId, options);
                MessagePackSerializer.Serialize(ref writer, value.ScanTimestamp, options);
                writer.Write(value.LowerId);
                writer.Write(value.Identity);
                writer.Write(value.Id);
                writer.Write(value.Version);
                MessagePackSerializer.Serialize(ref writer, value.CatalogCommitTimestamp, options);
                MessagePackSerializer.Serialize(ref writer, value.Created, options);
                writer.Write((int)value.ResultType);
                MessagePackSerializer.Serialize(ref writer, value.SequenceNumber, options);
                writer.Write(value.Path);
                writer.Write(value.FileName);
                writer.Write(value.FileExtension);
                writer.Write(value.TopLevelFolder);
                MessagePackSerializer.Serialize(ref writer, value.FileLength, options);
                MessagePackSerializer.Serialize(ref writer, value.EdgeCases, options);
                writer.Write(value.AssemblyName);
                MessagePackSerializer.Serialize(ref writer, value.AssemblyVersion, options);
                writer.Write(value.Culture);
                writer.Write(value.PublicKeyToken);
                MessagePackSerializer.Serialize(ref writer, value.HashAlgorithm, options);
                MessagePackSerializer.Serialize(ref writer, value.HasPublicKey, options);
                MessagePackSerializer.Serialize(ref writer, value.PublicKeyLength, options);
                writer.Write(value.PublicKeySHA1);
                writer.Write(value.CustomAttributes);
                writer.Write(value.CustomAttributesFailedDecode);
                MessagePackSerializer.Serialize(ref writer, value.CustomAttributesTotalCount, options);
                MessagePackSerializer.Serialize(ref writer, value.CustomAttributesTotalDataLength, options);
            }

            public PackageAssembly Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                var count = reader.ReadArrayHeader();
                if (count != 28)
                {
                    throw new MessagePackSerializationException($"Invalid array length: {count}");
                }

                var record = new PackageAssembly();

                record.ScanId = MessagePackSerializer.Deserialize<System.Guid?>(ref reader, options);
                record.ScanTimestamp = MessagePackSerializer.Deserialize<System.DateTimeOffset?>(ref reader, options);
                record.LowerId = reader.ReadString();
                record.Identity = reader.ReadString();
                record.Id = reader.ReadString();
                record.Version = reader.ReadString();
                record.CatalogCommitTimestamp = MessagePackSerializer.Deserialize<System.DateTimeOffset>(ref reader, options);
                record.Created = MessagePackSerializer.Deserialize<System.DateTimeOffset?>(ref reader, options);
                record.ResultType = (NuGet.Insights.Worker.PackageAssemblyToCsv.PackageAssemblyResultType)reader.ReadInt32();
                record.SequenceNumber = MessagePackSerializer.Deserialize<int?>(ref reader, options);
                record.Path = reader.ReadString();
                record.FileName = reader.ReadString();
                record.FileExtension = reader.ReadString();
                record.TopLevelFolder = reader.ReadString();
                record.FileLength = MessagePackSerializer.Deserialize<long?>(ref reader, options);
                record.EdgeCases = MessagePackSerializer.Deserialize<NuGet.Insights.Worker.PackageAssemblyToCsv.PackageAssemblyEdgeCases?>(ref reader, options);
                record.AssemblyName = reader.ReadString();
                record.AssemblyVersion = MessagePackSerializer.Deserialize<System.Version>(ref reader, options);
                record.Culture = reader.ReadString();
                record.PublicKeyToken = reader.ReadString();
                record.HashAlgorithm = MessagePackSerializer.Deserialize<System.Reflection.AssemblyHashAlgorithm?>(ref reader, options);
                record.HasPublicKey = MessagePackSerializer.Deserialize<bool?>(ref reader, options);
                record.PublicKeyLength = MessagePackSerializer.Deserialize<int?>(ref reader, options);
                record.PublicKeySHA1 = reader.ReadString();
                record.CustomAttributes = reader.ReadString();
                record.CustomAttributesFailedDecode = reader.ReadString();
                record.CustomAttributesTotalCount = MessagePackSerializer.Deserialize<int?>(ref reader, options);
                record.CustomAttributesTotalDataLength = MessagePackSerializer.Deserialize<int?>(ref reader, options);

                return record;
            }
        }
    }
}
﻿// <auto-generated />

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;
using MessagePack.Formatters;
using NuGet.Insights;

namespace NuGet.Insights.Worker.PackageLicenseToCsv
{
    partial record PackageLicense
    {
        public class PackageLicenseMessagePackFormatter : IMessagePackFormatter<PackageLicense>
        {
            public void Serialize(ref MessagePackWriter writer, PackageLicense value, MessagePackSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNil();
                    return;
                }

                writer.WriteArrayHeader(21);

                MessagePackSerializer.Serialize(ref writer, value.ScanId, options);
                MessagePackSerializer.Serialize(ref writer, value.ScanTimestamp, options);
                writer.Write(value.LowerId);
                writer.Write(value.Identity);
                writer.Write(value.Id);
                writer.Write(value.Version);
                MessagePackSerializer.Serialize(ref writer, value.CatalogCommitTimestamp, options);
                MessagePackSerializer.Serialize(ref writer, value.Created, options);
                writer.Write((int)value.ResultType);
                writer.Write(value.Url);
                writer.Write(value.Expression);
                writer.Write(value.File);
                writer.Write(value.GeneratedUrl);
                writer.Write(value.ExpressionParsed);
                writer.Write(value.ExpressionLicenses);
                writer.Write(value.ExpressionExceptions);
                writer.Write(value.ExpressionNonStandardLicenses);
                MessagePackSerializer.Serialize(ref writer, value.ExpressionHasDeprecatedIdentifier, options);
                MessagePackSerializer.Serialize(ref writer, value.FileLength, options);
                writer.Write(value.FileSHA256);
                writer.Write(value.FileContent);
            }

            public PackageLicense Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                var count = reader.ReadArrayHeader();
                if (count != 21)
                {
                    throw new MessagePackSerializationException($"Invalid array length: {count}");
                }

                var record = new PackageLicense();

                record.ScanId = MessagePackSerializer.Deserialize<System.Guid?>(ref reader, options);
                record.ScanTimestamp = MessagePackSerializer.Deserialize<System.DateTimeOffset?>(ref reader, options);
                record.LowerId = reader.ReadString();
                record.Identity = reader.ReadString();
                record.Id = reader.ReadString();
                record.Version = reader.ReadString();
                record.CatalogCommitTimestamp = MessagePackSerializer.Deserialize<System.DateTimeOffset>(ref reader, options);
                record.Created = MessagePackSerializer.Deserialize<System.DateTimeOffset?>(ref reader, options);
                record.ResultType = (NuGet.Insights.Worker.PackageLicenseToCsv.PackageLicenseResultType)reader.ReadInt32();
                record.Url = reader.ReadString();
                record.Expression = reader.ReadString();
                record.File = reader.ReadString();
                record.GeneratedUrl = reader.ReadString();
                record.ExpressionParsed = reader.ReadString();
                record.ExpressionLicenses = reader.ReadString();
                record.ExpressionExceptions = reader.ReadString();
                record.ExpressionNonStandardLicenses = reader.ReadString();
                record.ExpressionHasDeprecatedIdentifier = MessagePackSerializer.Deserialize<bool?>(ref reader, options);
                record.FileLength = MessagePackSerializer.Deserialize<long?>(ref reader, options);
                record.FileSHA256 = reader.ReadString();
                record.FileContent = reader.ReadString();

                return record;
            }
        }
    }
}
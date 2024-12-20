﻿// <auto-generated />

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Insights;

namespace NuGet.Insights.Worker.PackageIconToCsv
{
    /* Kusto DDL:

    .drop table PackageIcons ifexists;

    .create table PackageIcons (
        LowerId: string,
        Identity: string,
        Id: string,
        Version: string,
        CatalogCommitTimestamp: datetime,
        Created: datetime,
        ResultType: string,
        FileLength: long,
        FileSHA256: string,
        ContentType: string,
        HeaderFormat: string,
        AutoDetectedFormat: bool,
        Signature: string,
        Width: long,
        Height: long,
        FrameCount: int,
        IsOpaque: bool,
        FrameFormats: dynamic,
        FrameDimensions: dynamic,
        FrameAttributeNames: dynamic
    ) with (docstring = "See https://github.com/NuGet/Insights/blob/main/docs/tables/PackageIcons.md", folder = "");

    .alter-merge table PackageIcons policy retention softdelete = 30d;

    .alter table PackageIcons policy partitioning '{'
      '"PartitionKeys": ['
        '{'
          '"ColumnName": "Identity",'
          '"Kind": "Hash",'
          '"Properties": {'
            '"Function": "XxHash64",'
            '"MaxPartitionCount": 256'
          '}'
        '}'
      ']'
    '}';

    .create table PackageIcons ingestion csv mapping 'BlobStorageMapping'
    '['
        '{"Column":"LowerId","DataType":"string","Properties":{"Ordinal":2}},'
        '{"Column":"Identity","DataType":"string","Properties":{"Ordinal":3}},'
        '{"Column":"Id","DataType":"string","Properties":{"Ordinal":4}},'
        '{"Column":"Version","DataType":"string","Properties":{"Ordinal":5}},'
        '{"Column":"CatalogCommitTimestamp","DataType":"datetime","Properties":{"Ordinal":6}},'
        '{"Column":"Created","DataType":"datetime","Properties":{"Ordinal":7}},'
        '{"Column":"ResultType","DataType":"string","Properties":{"Ordinal":8}},'
        '{"Column":"FileLength","DataType":"long","Properties":{"Ordinal":9}},'
        '{"Column":"FileSHA256","DataType":"string","Properties":{"Ordinal":10}},'
        '{"Column":"ContentType","DataType":"string","Properties":{"Ordinal":11}},'
        '{"Column":"HeaderFormat","DataType":"string","Properties":{"Ordinal":12}},'
        '{"Column":"AutoDetectedFormat","DataType":"bool","Properties":{"Ordinal":13}},'
        '{"Column":"Signature","DataType":"string","Properties":{"Ordinal":14}},'
        '{"Column":"Width","DataType":"long","Properties":{"Ordinal":15}},'
        '{"Column":"Height","DataType":"long","Properties":{"Ordinal":16}},'
        '{"Column":"FrameCount","DataType":"int","Properties":{"Ordinal":17}},'
        '{"Column":"IsOpaque","DataType":"bool","Properties":{"Ordinal":18}},'
        '{"Column":"FrameFormats","DataType":"dynamic","Properties":{"Ordinal":19}},'
        '{"Column":"FrameDimensions","DataType":"dynamic","Properties":{"Ordinal":20}},'
        '{"Column":"FrameAttributeNames","DataType":"dynamic","Properties":{"Ordinal":21}}'
    ']'

    */
    partial record PackageIcon
    {
        public static int FieldCount => 22;

        public static void WriteHeader(TextWriter writer)
        {
            writer.WriteLine("ScanId,ScanTimestamp,LowerId,Identity,Id,Version,CatalogCommitTimestamp,Created,ResultType,FileLength,FileSHA256,ContentType,HeaderFormat,AutoDetectedFormat,Signature,Width,Height,FrameCount,IsOpaque,FrameFormats,FrameDimensions,FrameAttributeNames");
        }

        public void Write(List<string> fields)
        {
            fields.Add(ScanId.ToString());
            fields.Add(CsvUtility.FormatDateTimeOffset(ScanTimestamp));
            fields.Add(LowerId);
            fields.Add(Identity);
            fields.Add(Id);
            fields.Add(Version);
            fields.Add(CsvUtility.FormatDateTimeOffset(CatalogCommitTimestamp));
            fields.Add(CsvUtility.FormatDateTimeOffset(Created));
            fields.Add(ResultType.ToString());
            fields.Add(FileLength.ToString());
            fields.Add(FileSHA256);
            fields.Add(ContentType);
            fields.Add(HeaderFormat);
            fields.Add(CsvUtility.FormatBool(AutoDetectedFormat));
            fields.Add(Signature);
            fields.Add(Width.ToString());
            fields.Add(Height.ToString());
            fields.Add(FrameCount.ToString());
            fields.Add(CsvUtility.FormatBool(IsOpaque));
            fields.Add(FrameFormats);
            fields.Add(FrameDimensions);
            fields.Add(FrameAttributeNames);
        }

        public void Write(TextWriter writer)
        {
            writer.Write(ScanId);
            writer.Write(',');
            writer.Write(CsvUtility.FormatDateTimeOffset(ScanTimestamp));
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, LowerId);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, Identity);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, Id);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, Version);
            writer.Write(',');
            writer.Write(CsvUtility.FormatDateTimeOffset(CatalogCommitTimestamp));
            writer.Write(',');
            writer.Write(CsvUtility.FormatDateTimeOffset(Created));
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, ResultType.ToString());
            writer.Write(',');
            writer.Write(FileLength);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, FileSHA256);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, ContentType);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, HeaderFormat);
            writer.Write(',');
            writer.Write(CsvUtility.FormatBool(AutoDetectedFormat));
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, Signature);
            writer.Write(',');
            writer.Write(Width);
            writer.Write(',');
            writer.Write(Height);
            writer.Write(',');
            writer.Write(FrameCount);
            writer.Write(',');
            writer.Write(CsvUtility.FormatBool(IsOpaque));
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, FrameFormats);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, FrameDimensions);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, FrameAttributeNames);
            writer.WriteLine();
        }

        public async Task WriteAsync(TextWriter writer)
        {
            await writer.WriteAsync(ScanId.ToString());
            await writer.WriteAsync(',');
            await writer.WriteAsync(CsvUtility.FormatDateTimeOffset(ScanTimestamp));
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, LowerId);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, Identity);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, Id);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, Version);
            await writer.WriteAsync(',');
            await writer.WriteAsync(CsvUtility.FormatDateTimeOffset(CatalogCommitTimestamp));
            await writer.WriteAsync(',');
            await writer.WriteAsync(CsvUtility.FormatDateTimeOffset(Created));
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, ResultType.ToString());
            await writer.WriteAsync(',');
            await writer.WriteAsync(FileLength.ToString());
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, FileSHA256);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, ContentType);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, HeaderFormat);
            await writer.WriteAsync(',');
            await writer.WriteAsync(CsvUtility.FormatBool(AutoDetectedFormat));
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, Signature);
            await writer.WriteAsync(',');
            await writer.WriteAsync(Width.ToString());
            await writer.WriteAsync(',');
            await writer.WriteAsync(Height.ToString());
            await writer.WriteAsync(',');
            await writer.WriteAsync(FrameCount.ToString());
            await writer.WriteAsync(',');
            await writer.WriteAsync(CsvUtility.FormatBool(IsOpaque));
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, FrameFormats);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, FrameDimensions);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, FrameAttributeNames);
            await writer.WriteLineAsync();
        }

        public static PackageIcon ReadNew(Func<string> getNextField)
        {
            return new PackageIcon
            {
                ScanId = CsvUtility.ParseNullable(getNextField(), Guid.Parse),
                ScanTimestamp = CsvUtility.ParseNullable(getNextField(), CsvUtility.ParseDateTimeOffset),
                LowerId = getNextField(),
                Identity = getNextField(),
                Id = getNextField(),
                Version = getNextField(),
                CatalogCommitTimestamp = CsvUtility.ParseDateTimeOffset(getNextField()),
                Created = CsvUtility.ParseNullable(getNextField(), CsvUtility.ParseDateTimeOffset),
                ResultType = Enum.Parse<PackageIconResultType>(getNextField()),
                FileLength = CsvUtility.ParseNullable(getNextField(), long.Parse),
                FileSHA256 = getNextField(),
                ContentType = getNextField(),
                HeaderFormat = getNextField(),
                AutoDetectedFormat = CsvUtility.ParseNullable(getNextField(), bool.Parse),
                Signature = getNextField(),
                Width = CsvUtility.ParseNullable(getNextField(), long.Parse),
                Height = CsvUtility.ParseNullable(getNextField(), long.Parse),
                FrameCount = CsvUtility.ParseNullable(getNextField(), int.Parse),
                IsOpaque = CsvUtility.ParseNullable(getNextField(), bool.Parse),
                FrameFormats = getNextField(),
                FrameDimensions = getNextField(),
                FrameAttributeNames = getNextField(),
            };
        }

        public void SetEmptyStrings()
        {
            if (LowerId is null)
            {
                LowerId = string.Empty;
            }

            if (Identity is null)
            {
                Identity = string.Empty;
            }

            if (Id is null)
            {
                Id = string.Empty;
            }

            if (Version is null)
            {
                Version = string.Empty;
            }

            if (FileSHA256 is null)
            {
                FileSHA256 = string.Empty;
            }

            if (ContentType is null)
            {
                ContentType = string.Empty;
            }

            if (HeaderFormat is null)
            {
                HeaderFormat = string.Empty;
            }

            if (Signature is null)
            {
                Signature = string.Empty;
            }

            if (FrameFormats is null)
            {
                FrameFormats = string.Empty;
            }

            if (FrameDimensions is null)
            {
                FrameDimensions = string.Empty;
            }

            if (FrameAttributeNames is null)
            {
                FrameAttributeNames = string.Empty;
            }
        }

        public string GetBucketKey()
        {
            return Identity;
        }
    }
}

﻿// <auto-generated />

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Insights;

namespace NuGet.Insights.Worker.CatalogDataToCsv
{
    /* Kusto DDL:

    .drop table PackageDeprecations ifexists;

    .create table PackageDeprecations (
        LowerId: string,
        Identity: string,
        Id: string,
        Version: string,
        CatalogCommitTimestamp: datetime,
        Created: datetime,
        ResultType: string,
        Message: string,
        Reasons: dynamic,
        AlternatePackageId: string,
        AlternateVersionRange: string
    ) with (docstring = "See https://github.com/NuGet/Insights/blob/main/docs/tables/PackageDeprecations.md", folder = "");

    .alter-merge table PackageDeprecations policy retention softdelete = 30d;

    .alter table PackageDeprecations policy partitioning '{'
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

    .create table PackageDeprecations ingestion csv mapping 'BlobStorageMapping'
    '['
        '{"Column":"LowerId","DataType":"string","Properties":{"Ordinal":2}},'
        '{"Column":"Identity","DataType":"string","Properties":{"Ordinal":3}},'
        '{"Column":"Id","DataType":"string","Properties":{"Ordinal":4}},'
        '{"Column":"Version","DataType":"string","Properties":{"Ordinal":5}},'
        '{"Column":"CatalogCommitTimestamp","DataType":"datetime","Properties":{"Ordinal":6}},'
        '{"Column":"Created","DataType":"datetime","Properties":{"Ordinal":7}},'
        '{"Column":"ResultType","DataType":"string","Properties":{"Ordinal":8}},'
        '{"Column":"Message","DataType":"string","Properties":{"Ordinal":9}},'
        '{"Column":"Reasons","DataType":"dynamic","Properties":{"Ordinal":10}},'
        '{"Column":"AlternatePackageId","DataType":"string","Properties":{"Ordinal":11}},'
        '{"Column":"AlternateVersionRange","DataType":"string","Properties":{"Ordinal":12}}'
    ']'

    */
    partial record PackageDeprecationRecord
    {
        public static int FieldCount => 13;

        public static void WriteHeader(TextWriter writer)
        {
            writer.WriteLine("ScanId,ScanTimestamp,LowerId,Identity,Id,Version,CatalogCommitTimestamp,Created,ResultType,Message,Reasons,AlternatePackageId,AlternateVersionRange");
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
            fields.Add(Message);
            fields.Add(Reasons);
            fields.Add(AlternatePackageId);
            fields.Add(AlternateVersionRange);
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
            CsvUtility.WriteWithQuotes(writer, Message);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, Reasons);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, AlternatePackageId);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, AlternateVersionRange);
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
            await CsvUtility.WriteWithQuotesAsync(writer, Message);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, Reasons);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, AlternatePackageId);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, AlternateVersionRange);
            await writer.WriteLineAsync();
        }

        public static PackageDeprecationRecord ReadNew(Func<string> getNextField)
        {
            return new PackageDeprecationRecord
            {
                ScanId = CsvUtility.ParseNullable(getNextField(), Guid.Parse),
                ScanTimestamp = CsvUtility.ParseNullable(getNextField(), CsvUtility.ParseDateTimeOffset),
                LowerId = getNextField(),
                Identity = getNextField(),
                Id = getNextField(),
                Version = getNextField(),
                CatalogCommitTimestamp = CsvUtility.ParseDateTimeOffset(getNextField()),
                Created = CsvUtility.ParseNullable(getNextField(), CsvUtility.ParseDateTimeOffset),
                ResultType = Enum.Parse<PackageDeprecationResultType>(getNextField()),
                Message = getNextField(),
                Reasons = getNextField(),
                AlternatePackageId = getNextField(),
                AlternateVersionRange = getNextField(),
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

            if (Message is null)
            {
                Message = string.Empty;
            }

            if (Reasons is null)
            {
                Reasons = string.Empty;
            }

            if (AlternatePackageId is null)
            {
                AlternatePackageId = string.Empty;
            }

            if (AlternateVersionRange is null)
            {
                AlternateVersionRange = string.Empty;
            }
        }

        public string GetBucketKey()
        {
            return Identity;
        }
    }
}

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

    .drop table CatalogLeafItems ifexists;

    .create table CatalogLeafItems (
        CommitId: string,
        CommitTimestamp: datetime,
        LowerId: string,
        Identity: string,
        Id: string,
        Version: string,
        Type: string,
        Url: string,
        PageUrl: string,
        Published: datetime,
        IsListed: bool,
        Created: datetime,
        LastEdited: datetime,
        PackageSize: long,
        PackageHash: string,
        PackageHashAlgorithm: string,
        Deprecation: dynamic,
        Vulnerabilities: dynamic,
        HasRepositoryProperty: bool,
        PackageEntryCount: int,
        NuspecPackageEntry: dynamic,
        SignaturePackageEntry: dynamic
    ) with (docstring = "See https://github.com/NuGet/Insights/blob/main/docs/tables/CatalogLeafItems.md", folder = "");

    .alter-merge table CatalogLeafItems policy retention softdelete = 30d;

    .alter table CatalogLeafItems policy partitioning '{'
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

    .create table CatalogLeafItems ingestion csv mapping 'BlobStorageMapping'
    '['
        '{"Column":"CommitId","DataType":"string","Properties":{"Ordinal":0}},'
        '{"Column":"CommitTimestamp","DataType":"datetime","Properties":{"Ordinal":1}},'
        '{"Column":"LowerId","DataType":"string","Properties":{"Ordinal":2}},'
        '{"Column":"Identity","DataType":"string","Properties":{"Ordinal":3}},'
        '{"Column":"Id","DataType":"string","Properties":{"Ordinal":4}},'
        '{"Column":"Version","DataType":"string","Properties":{"Ordinal":5}},'
        '{"Column":"Type","DataType":"string","Properties":{"Ordinal":6}},'
        '{"Column":"Url","DataType":"string","Properties":{"Ordinal":7}},'
        '{"Column":"PageUrl","DataType":"string","Properties":{"Ordinal":8}},'
        '{"Column":"Published","DataType":"datetime","Properties":{"Ordinal":9}},'
        '{"Column":"IsListed","DataType":"bool","Properties":{"Ordinal":10}},'
        '{"Column":"Created","DataType":"datetime","Properties":{"Ordinal":11}},'
        '{"Column":"LastEdited","DataType":"datetime","Properties":{"Ordinal":12}},'
        '{"Column":"PackageSize","DataType":"long","Properties":{"Ordinal":13}},'
        '{"Column":"PackageHash","DataType":"string","Properties":{"Ordinal":14}},'
        '{"Column":"PackageHashAlgorithm","DataType":"string","Properties":{"Ordinal":15}},'
        '{"Column":"Deprecation","DataType":"dynamic","Properties":{"Ordinal":16}},'
        '{"Column":"Vulnerabilities","DataType":"dynamic","Properties":{"Ordinal":17}},'
        '{"Column":"HasRepositoryProperty","DataType":"bool","Properties":{"Ordinal":18}},'
        '{"Column":"PackageEntryCount","DataType":"int","Properties":{"Ordinal":19}},'
        '{"Column":"NuspecPackageEntry","DataType":"dynamic","Properties":{"Ordinal":20}},'
        '{"Column":"SignaturePackageEntry","DataType":"dynamic","Properties":{"Ordinal":21}}'
    ']'

    */
    partial record CatalogLeafItemRecord
    {
        public static int FieldCount => 22;

        public static void WriteHeader(TextWriter writer)
        {
            writer.WriteLine("CommitId,CommitTimestamp,LowerId,Identity,Id,Version,Type,Url,PageUrl,Published,IsListed,Created,LastEdited,PackageSize,PackageHash,PackageHashAlgorithm,Deprecation,Vulnerabilities,HasRepositoryProperty,PackageEntryCount,NuspecPackageEntry,SignaturePackageEntry");
        }

        public void Write(List<string> fields)
        {
            fields.Add(CommitId);
            fields.Add(CsvUtility.FormatDateTimeOffset(CommitTimestamp));
            fields.Add(LowerId);
            fields.Add(Identity);
            fields.Add(Id);
            fields.Add(Version);
            fields.Add(Type.ToString());
            fields.Add(Url);
            fields.Add(PageUrl);
            fields.Add(CsvUtility.FormatDateTimeOffset(Published));
            fields.Add(CsvUtility.FormatBool(IsListed));
            fields.Add(CsvUtility.FormatDateTimeOffset(Created));
            fields.Add(CsvUtility.FormatDateTimeOffset(LastEdited));
            fields.Add(PackageSize.ToString());
            fields.Add(PackageHash);
            fields.Add(PackageHashAlgorithm);
            fields.Add(Deprecation);
            fields.Add(Vulnerabilities);
            fields.Add(CsvUtility.FormatBool(HasRepositoryProperty));
            fields.Add(PackageEntryCount.ToString());
            fields.Add(NuspecPackageEntry);
            fields.Add(SignaturePackageEntry);
        }

        public void Write(TextWriter writer)
        {
            CsvUtility.WriteWithQuotes(writer, CommitId);
            writer.Write(',');
            writer.Write(CsvUtility.FormatDateTimeOffset(CommitTimestamp));
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, LowerId);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, Identity);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, Id);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, Version);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, Type.ToString());
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, Url);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, PageUrl);
            writer.Write(',');
            writer.Write(CsvUtility.FormatDateTimeOffset(Published));
            writer.Write(',');
            writer.Write(CsvUtility.FormatBool(IsListed));
            writer.Write(',');
            writer.Write(CsvUtility.FormatDateTimeOffset(Created));
            writer.Write(',');
            writer.Write(CsvUtility.FormatDateTimeOffset(LastEdited));
            writer.Write(',');
            writer.Write(PackageSize);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, PackageHash);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, PackageHashAlgorithm);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, Deprecation);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, Vulnerabilities);
            writer.Write(',');
            writer.Write(CsvUtility.FormatBool(HasRepositoryProperty));
            writer.Write(',');
            writer.Write(PackageEntryCount);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, NuspecPackageEntry);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, SignaturePackageEntry);
            writer.WriteLine();
        }

        public async Task WriteAsync(TextWriter writer)
        {
            await CsvUtility.WriteWithQuotesAsync(writer, CommitId);
            await writer.WriteAsync(',');
            await writer.WriteAsync(CsvUtility.FormatDateTimeOffset(CommitTimestamp));
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, LowerId);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, Identity);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, Id);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, Version);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, Type.ToString());
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, Url);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, PageUrl);
            await writer.WriteAsync(',');
            await writer.WriteAsync(CsvUtility.FormatDateTimeOffset(Published));
            await writer.WriteAsync(',');
            await writer.WriteAsync(CsvUtility.FormatBool(IsListed));
            await writer.WriteAsync(',');
            await writer.WriteAsync(CsvUtility.FormatDateTimeOffset(Created));
            await writer.WriteAsync(',');
            await writer.WriteAsync(CsvUtility.FormatDateTimeOffset(LastEdited));
            await writer.WriteAsync(',');
            await writer.WriteAsync(PackageSize.ToString());
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, PackageHash);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, PackageHashAlgorithm);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, Deprecation);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, Vulnerabilities);
            await writer.WriteAsync(',');
            await writer.WriteAsync(CsvUtility.FormatBool(HasRepositoryProperty));
            await writer.WriteAsync(',');
            await writer.WriteAsync(PackageEntryCount.ToString());
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, NuspecPackageEntry);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, SignaturePackageEntry);
            await writer.WriteLineAsync();
        }

        public static CatalogLeafItemRecord ReadNew(Func<string> getNextField)
        {
            return new CatalogLeafItemRecord
            {
                CommitId = getNextField(),
                CommitTimestamp = CsvUtility.ParseDateTimeOffset(getNextField()),
                LowerId = getNextField(),
                Identity = getNextField(),
                Id = getNextField(),
                Version = getNextField(),
                Type = Enum.Parse<NuGet.Insights.CatalogLeafType>(getNextField()),
                Url = getNextField(),
                PageUrl = getNextField(),
                Published = CsvUtility.ParseNullable(getNextField(), CsvUtility.ParseDateTimeOffset),
                IsListed = CsvUtility.ParseNullable(getNextField(), bool.Parse),
                Created = CsvUtility.ParseNullable(getNextField(), CsvUtility.ParseDateTimeOffset),
                LastEdited = CsvUtility.ParseNullable(getNextField(), CsvUtility.ParseDateTimeOffset),
                PackageSize = CsvUtility.ParseNullable(getNextField(), long.Parse),
                PackageHash = getNextField(),
                PackageHashAlgorithm = getNextField(),
                Deprecation = getNextField(),
                Vulnerabilities = getNextField(),
                HasRepositoryProperty = CsvUtility.ParseNullable(getNextField(), bool.Parse),
                PackageEntryCount = CsvUtility.ParseNullable(getNextField(), int.Parse),
                NuspecPackageEntry = getNextField(),
                SignaturePackageEntry = getNextField(),
            };
        }

        public void SetEmptyStrings()
        {
            if (CommitId is null)
            {
                CommitId = string.Empty;
            }

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

            if (Url is null)
            {
                Url = string.Empty;
            }

            if (PageUrl is null)
            {
                PageUrl = string.Empty;
            }

            if (PackageHash is null)
            {
                PackageHash = string.Empty;
            }

            if (PackageHashAlgorithm is null)
            {
                PackageHashAlgorithm = string.Empty;
            }

            if (Deprecation is null)
            {
                Deprecation = string.Empty;
            }

            if (Vulnerabilities is null)
            {
                Vulnerabilities = string.Empty;
            }

            if (NuspecPackageEntry is null)
            {
                NuspecPackageEntry = string.Empty;
            }

            if (SignaturePackageEntry is null)
            {
                SignaturePackageEntry = string.Empty;
            }
        }

        public string GetBucketKey()
        {
            return Identity;
        }
    }
}

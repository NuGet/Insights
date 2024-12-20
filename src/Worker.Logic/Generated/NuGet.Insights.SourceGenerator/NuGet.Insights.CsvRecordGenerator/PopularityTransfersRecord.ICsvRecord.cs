﻿// <auto-generated />

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Insights;

namespace NuGet.Insights.Worker.PopularityTransfersToCsv
{
    /* Kusto DDL:

    .drop table PopularityTransfers ifexists;

    .create table PopularityTransfers (
        LowerId: string,
        Id: string,
        TransferIds: dynamic,
        TransferLowerIds: dynamic
    ) with (docstring = "See https://github.com/NuGet/Insights/blob/main/docs/tables/PopularityTransfers.md", folder = "");

    .alter-merge table PopularityTransfers policy retention softdelete = 30d;

    .alter table PopularityTransfers policy partitioning '{'
      '"PartitionKeys": ['
        '{'
          '"ColumnName": "LowerId",'
          '"Kind": "Hash",'
          '"Properties": {'
            '"Function": "XxHash64",'
            '"MaxPartitionCount": 256'
          '}'
        '}'
      ']'
    '}';

    .create table PopularityTransfers ingestion csv mapping 'BlobStorageMapping'
    '['
        '{"Column":"LowerId","DataType":"string","Properties":{"Ordinal":1}},'
        '{"Column":"Id","DataType":"string","Properties":{"Ordinal":2}},'
        '{"Column":"TransferIds","DataType":"dynamic","Properties":{"Ordinal":3}},'
        '{"Column":"TransferLowerIds","DataType":"dynamic","Properties":{"Ordinal":4}}'
    ']'

    */
    partial record PopularityTransfersRecord
    {
        public static int FieldCount => 5;

        public static void WriteHeader(TextWriter writer)
        {
            writer.WriteLine("AsOfTimestamp,LowerId,Id,TransferIds,TransferLowerIds");
        }

        public void Write(List<string> fields)
        {
            fields.Add(CsvUtility.FormatDateTimeOffset(AsOfTimestamp));
            fields.Add(LowerId);
            fields.Add(Id);
            fields.Add(TransferIds);
            fields.Add(TransferLowerIds);
        }

        public void Write(TextWriter writer)
        {
            writer.Write(CsvUtility.FormatDateTimeOffset(AsOfTimestamp));
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, LowerId);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, Id);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, TransferIds);
            writer.Write(',');
            CsvUtility.WriteWithQuotes(writer, TransferLowerIds);
            writer.WriteLine();
        }

        public async Task WriteAsync(TextWriter writer)
        {
            await writer.WriteAsync(CsvUtility.FormatDateTimeOffset(AsOfTimestamp));
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, LowerId);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, Id);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, TransferIds);
            await writer.WriteAsync(',');
            await CsvUtility.WriteWithQuotesAsync(writer, TransferLowerIds);
            await writer.WriteLineAsync();
        }

        public static PopularityTransfersRecord ReadNew(Func<string> getNextField)
        {
            return new PopularityTransfersRecord
            {
                AsOfTimestamp = CsvUtility.ParseDateTimeOffset(getNextField()),
                LowerId = getNextField(),
                Id = getNextField(),
                TransferIds = getNextField(),
                TransferLowerIds = getNextField(),
            };
        }

        public void SetEmptyStrings()
        {
            if (LowerId is null)
            {
                LowerId = string.Empty;
            }

            if (Id is null)
            {
                Id = string.Empty;
            }

            if (TransferIds is null)
            {
                TransferIds = string.Empty;
            }

            if (TransferLowerIds is null)
            {
                TransferLowerIds = string.Empty;
            }
        }

        public string GetBucketKey()
        {
            return LowerId;
        }
    }
}

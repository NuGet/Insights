﻿// <auto-generated />

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Insights
{
    static partial class NuGetInsightsWorkerLogicKustoDDL
    {
        public const string PackageDownloadRecordDefaultTableName = "PackageDownloads";

        public static readonly IReadOnlyList<string> PackageDownloadRecordDDL = new[]
        {
            ".drop table __TABLENAME__ ifexists",

            """
            .create table __TABLENAME__ (
                LowerId: string,
                Identity: string,
                Id: string,
                Version: string,
                Downloads: long,
                TotalDownloads: long
            ) with (docstring = __DOCSTRING__, folder = __FOLDER__)
            """,

            ".alter-merge table __TABLENAME__ policy retention softdelete = 30d",

            """
            .create table __TABLENAME__ ingestion csv mapping 'BlobStorageMapping'
            '['
                '{"Column":"LowerId","DataType":"string","Properties":{"Ordinal":1}},'
                '{"Column":"Identity","DataType":"string","Properties":{"Ordinal":2}},'
                '{"Column":"Id","DataType":"string","Properties":{"Ordinal":3}},'
                '{"Column":"Version","DataType":"string","Properties":{"Ordinal":4}},'
                '{"Column":"Downloads","DataType":"long","Properties":{"Ordinal":5}},'
                '{"Column":"TotalDownloads","DataType":"long","Properties":{"Ordinal":6}}'
            ']'
            """,
        };

        public const string PackageDownloadRecordPartitioningPolicy =
            """
            .alter table __TABLENAME__ policy partitioning '{'
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
            '}'
            """;

        private static readonly bool PackageDownloadRecordAddTypeToDefaultTableName = AddTypeToDefaultTableName(typeof(NuGet.Insights.Worker.DownloadsToCsv.PackageDownloadRecord), PackageDownloadRecordDefaultTableName);

        private static readonly bool PackageDownloadRecordAddTypeToDDL = AddTypeToDDL(typeof(NuGet.Insights.Worker.DownloadsToCsv.PackageDownloadRecord), PackageDownloadRecordDDL);

        private static readonly bool PackageDownloadRecordAddTypeToPartitioningPolicy = AddTypeToPartitioningPolicy(typeof(NuGet.Insights.Worker.DownloadsToCsv.PackageDownloadRecord), PackageDownloadRecordPartitioningPolicy);
    }
}

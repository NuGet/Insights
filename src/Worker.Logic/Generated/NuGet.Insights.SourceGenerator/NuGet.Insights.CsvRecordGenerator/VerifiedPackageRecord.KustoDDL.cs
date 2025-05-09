﻿// <auto-generated />

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Insights
{
    static partial class NuGetInsightsWorkerLogicKustoDDL
    {
        public const string VerifiedPackageRecordDefaultTableName = "VerifiedPackages";

        public static readonly IReadOnlyList<string> VerifiedPackageRecordDDL = new[]
        {
            ".drop table __TABLENAME__ ifexists",

            """
            .create table __TABLENAME__ (
                LowerId: string,
                Id: string,
                IsVerified: bool
            ) with (docstring = __DOCSTRING__, folder = __FOLDER__)
            """,

            ".alter-merge table __TABLENAME__ policy retention softdelete = 30d",

            """
            .create table __TABLENAME__ ingestion csv mapping 'BlobStorageMapping'
            '['
                '{"Column":"LowerId","DataType":"string","Properties":{"Ordinal":1}},'
                '{"Column":"Id","DataType":"string","Properties":{"Ordinal":2}},'
                '{"Column":"IsVerified","DataType":"bool","Properties":{"Ordinal":3}}'
            ']'
            """,
        };

        public const string VerifiedPackageRecordPartitioningPolicy =
            """
            .alter table __TABLENAME__ policy partitioning '{'
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
            '}'
            """;

        private static readonly bool VerifiedPackageRecordAddTypeToDefaultTableName = AddTypeToDefaultTableName(typeof(NuGet.Insights.Worker.VerifiedPackagesToCsv.VerifiedPackageRecord), VerifiedPackageRecordDefaultTableName);

        private static readonly bool VerifiedPackageRecordAddTypeToDDL = AddTypeToDDL(typeof(NuGet.Insights.Worker.VerifiedPackagesToCsv.VerifiedPackageRecord), VerifiedPackageRecordDDL);

        private static readonly bool VerifiedPackageRecordAddTypeToPartitioningPolicy = AddTypeToPartitioningPolicy(typeof(NuGet.Insights.Worker.VerifiedPackagesToCsv.VerifiedPackageRecord), VerifiedPackageRecordPartitioningPolicy);
    }
}

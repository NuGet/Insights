// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public static class DiagnosticIds
    {
        public const string MissingICsvRecordInterface = "NI0001";
        public const string CsvRecordNotMarkedAsPartial = "NI0002";
        public const string MultipleKustoPartioningKeys = "NI0003";
        public const string NoKustoPartitioningKeyDefined = "NI0004";
        public const string IgnoredKustoPartitioningKey = "NI0005";
        public const string MissingKustoTypeAttribute = "NI0006";
        public const string MissingNoKustoDDLAttribute = "NI0007";
        public const string NonNullablePropertyNotMarkedAsRequired = "NI0008";
        public const string MultipleBucketKeys = "NI0009";
        public const string BucketKeyNotString = "NI0010";
        public const string NoBucketKeyDefined = "NI0011";
        public const string InvalidTargetType = "NI0012";
        public const string TargetTypeMustBePublic = "NI0013";
        public const string TargetTypeMustNotBeAbstract = "NI0014";
        public const string TargetTypeMustBePartial = "NI0015";
    }
}

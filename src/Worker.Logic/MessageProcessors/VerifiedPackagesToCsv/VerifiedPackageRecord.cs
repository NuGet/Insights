// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.VerifiedPackagesToCsv
{
    public partial record VerifiedPackageRecord : ICsvRecord<VerifiedPackageRecord>
    {
        [KustoIgnore]
        public DateTimeOffset AsOfTimestamp { get; set; }

        [KustoPartitionKey]
        public string LowerId { get; set; }

        public string Id { get; set; }

        [Required]
        public bool IsVerified { get; set; }
    }
}

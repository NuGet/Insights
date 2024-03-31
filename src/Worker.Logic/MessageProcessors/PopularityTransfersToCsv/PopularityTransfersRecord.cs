// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PopularityTransfersToCsv
{
    public partial record PopularityTransfersRecord : ICsvRecord
    {
        [KustoIgnore]
        public DateTimeOffset AsOfTimestamp { get; set; }

        [KustoPartitionKey]
        public string LowerId { get; set; }

        public string Id { get; set; }

        [KustoType("dynamic")]
        public string TransferIds { get; set; }

        [KustoType("dynamic")]
        public string TransferLowerIds { get; set; }
    }
}

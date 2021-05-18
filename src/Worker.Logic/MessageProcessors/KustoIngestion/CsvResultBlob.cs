// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class CsvResultBlob
    {
        public CsvResultBlob(string name, long rawSizeBytes)
        {
            Name = name;
            RawSizeBytes = rawSizeBytes;
        }

        public string Name { get; }
        public long RawSizeBytes { get; }
    }
}

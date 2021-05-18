// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Insights.Worker
{
    public class CsvReaderResult<T> where T : ICsvRecord
    {
        public CsvReaderResult(CsvReaderResultType type, List<T> records)
        {
            Type = type;
            Records = records;
        }

        public CsvReaderResultType Type { get; }
        public List<T> Records { get; }
    }
}

﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.Insights.Worker
{
    public interface ICsvReader
    {
        CsvReaderResult<T> GetRecords<T>(TextReader reader, int bufferSize) where T : ICsvRecord;
    }
}

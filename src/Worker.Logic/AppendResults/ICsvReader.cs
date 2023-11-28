// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public interface ICsvReader
    {
        string GetHeader<T>() where T : ICsvRecord;
        CsvReaderResult<T> GetRecords<T>(TextReader reader, int bufferSize) where T : ICsvRecord;
    }
}

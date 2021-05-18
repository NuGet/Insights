// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public interface ICsvRecord
    {
        int FieldCount { get; }
        void WriteHeader(TextWriter writer);
        void Write(List<string> fields);
        void Write(TextWriter writer);
        Task WriteAsync(TextWriter writer);
        ICsvRecord ReadNew(Func<string> getNextField);
    }
}

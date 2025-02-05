// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

#nullable enable

namespace NuGet.Insights
{
    public record CsvRecordResult(Diagnostic? Diagnostic, CsvRecordModel? Model)
    {
        public CsvRecordResult(Diagnostic diagnostic) : this(diagnostic, null)
        {
        }

        public CsvRecordResult(CsvRecordModel model) : this(null, model)
        {
        }
    }
}

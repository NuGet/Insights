// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace NuGet.Insights
{
    public interface IPropertyVisitor
    {
        void OnProperty(SourceProductionContext context, CsvRecordModel model, CsvPropertyModel property);
        void Finish(SourceProductionContext context, CsvRecordModel model);
        string GetResult();
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using McMaster.Extensions.CommandLineUtils;

namespace NuGet.Insights.Tool
{
    public interface ICommand
    {
        void Configure(CommandLineApplication app);
        Task ExecuteAsync(CancellationToken token);
    }
}

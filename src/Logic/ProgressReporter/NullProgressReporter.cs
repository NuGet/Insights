// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class NullProgressReporter : IProgressReporter
    {
        private static readonly Lazy<NullProgressReporter> LazyInstance
            = new Lazy<NullProgressReporter>(() => new NullProgressReporter());

        public static NullProgressReporter Instance => LazyInstance.Value;

        public Task ReportProgressAsync(decimal percent, string message)
        {
            return Task.CompletedTask;
        }
    }
}

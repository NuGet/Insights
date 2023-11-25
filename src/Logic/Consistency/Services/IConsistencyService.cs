// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public interface IConsistencyService<T> where T : IConsistencyReport
    {
        Task<T> GetReportAsync(PackageConsistencyContext context, PackageConsistencyState state, IProgressReporter progressReporter);
        Task<bool> IsConsistentAsync(PackageConsistencyContext context, PackageConsistencyState state, IProgressReporter progressReporter);
        Task PopulateStateAsync(PackageConsistencyContext context, PackageConsistencyState state, IProgressReporter progressReporter);
    }
}

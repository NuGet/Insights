// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.PackageCertificateToCsv;

namespace NuGet.Insights.Worker
{
    public static partial class ServiceCollectionExtensions
    {
        /// <summary>
        /// Invoked via reflection in <see cref="AddNuGetInsightsWorker(IServiceCollection)"/>.
        /// </summary>
        private static void SetupPackageCertificateToCsvDriver(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<PackageCertificateToCsvDriver>();
        }
    }
}
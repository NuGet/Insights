// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class RegistrationGzippedConsistencyService : RegistrationConsistencyService
    {
        public RegistrationGzippedConsistencyService(ServiceIndexCache serviceIndexCache, RegistrationClient client)
            : base(serviceIndexCache, client, type: ServiceIndexTypes.RegistrationGzipped, hasSemVer2: false)
        {
        }
    }
}

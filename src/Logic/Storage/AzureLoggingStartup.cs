// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

namespace NuGet.Insights
{
    public class AzureLoggingStartup
    {
        public AzureLoggingStartup(
            AzureEventSourceLogForwarder forwarder,
            IOptions<NuGetInsightsSettings> options)
        {
            if (options.Value.EnableAzureLogging)
            {
                forwarder.Start();
            }
        }
    }
}

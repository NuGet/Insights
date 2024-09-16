// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Http;
using Polly;

#nullable enable

namespace NuGet.Insights
{
    public class TimeoutHttpMessageHandler : PolicyHttpMessageHandler
    {
        public TimeoutHttpMessageHandler(IOptions<NuGetInsightsSettings> options) : base(GetPolicy(options))
        {
        }

        private static IAsyncPolicy<HttpResponseMessage> GetPolicy(IOptions<NuGetInsightsSettings> options)
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(options.Value.HttpClientNetworkTimeout);
        }
    }
}

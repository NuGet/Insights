// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker
{
    public class CustomNameResolver : DefaultNameResolver
    {
        private const string WorkQueueKey = nameof(CustomNameResolver) + ":WorkQueueName";
        private const string ExpandQueueKey = nameof(CustomNameResolver) + ":ExpandQueueName";
        public const string WorkQueueVariable = "%" + WorkQueueKey + "%";
        public const string ExpandQueueVariable = "%" + ExpandQueueKey + "%";

        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public CustomNameResolver(
            IConfiguration configuration,
            IOptions<NuGetInsightsWorkerSettings> options) : base(configuration)
        {
            _options = options;
        }

        public override string Resolve(string name)
        {
            switch (name)
            {
                case WorkQueueKey:
                    return _options.Value.WorkQueueName;
                case ExpandQueueKey:
                    return _options.Value.ExpandQueueName;
                default:
                    return base.Resolve(name);
            }
        }
    }
}

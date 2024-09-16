// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Worker
{
    public class ServiceCollectionExtensionsTest
    {
        [Fact]
        public void ValidateScopes()
        {
            var builder = new ServiceCollection();
            builder.AddSingleton<TelemetryClient>();
            builder.AddNuGetInsights();
            builder.AddNuGetInsightsWorker();

            builder.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
        }

        [Fact]
        public void DoesNotAddTypesASecondTime()
        {
            // Arrange
            var builder = new ServiceCollection();
            builder.AddNuGetInsightsWorker();
            Assert.Contains(builder, x => x.ServiceType == typeof(WorkflowService));
            builder.RemoveAll<WorkflowService>();

            // Act
            builder.AddNuGetInsightsWorker();

            // Assert
            Assert.DoesNotContain(builder, x => x.ServiceType == typeof(WorkflowService));
        }
    }
}

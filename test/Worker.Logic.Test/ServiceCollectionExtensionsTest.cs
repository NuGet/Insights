// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NuGet.Insights.Worker.Workflow;
using Xunit;

namespace NuGet.Insights.Worker
{
    public class ServiceCollectionExtensionsTest
    {
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

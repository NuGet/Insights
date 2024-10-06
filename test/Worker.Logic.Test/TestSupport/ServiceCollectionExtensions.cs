// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Kusto;

namespace NuGet.Insights.Worker
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMockKusto(this IServiceCollection serviceCollection, BaseWorkerLogicIntegrationTest test)
        {
            serviceCollection.AddSingleton(services =>
            {
                var mock = new Mock<CachingKustoClientFactory>(
                    services.GetRequiredService<IOptions<NuGetInsightsWorkerSettings>>(),
                    services.GetRequiredService<ILoggerFactory>())
                {
                    CallBase = true,
                };

                mock.Setup(x => x.GetAdminClientAsync()).ReturnsAsync(() => test.MockCslAdminProvider.Object);
                mock.Setup(x => x.GetQueryClientAsync()).ReturnsAsync(() => test.MockCslQueryProvider.Object);
                mock.Setup(x => x.GetIngestClientAsync()).ReturnsAsync(() => test.MockKustoQueueIngestClient.Object);

                return mock.Object;
            });

            return serviceCollection;
        }
    }
}

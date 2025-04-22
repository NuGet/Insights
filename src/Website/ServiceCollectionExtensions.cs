// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Authorization;

namespace NuGet.Insights.Website
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddNuGetInsightsWebsite(this IServiceCollection services)
        {
            services.AddSingleton<MoveMessagesTaskQueue>();
            services.AddSingleton<CachedAdminViewModelService.AdminViewModelCache>();
            services.AddSingleton<IAdminViewModelCache>(s => s.GetRequiredService<CachedAdminViewModelService.AdminViewModelCache>());
            services.AddScoped<IAuthorizationHandler, AllowListAuthorizationHandler>();
            services.AddScoped<AllowListAuthorizationHandler>();
            services.AddSingleton<ViewModelFactory>();
            services.AddHostedService(s => s.GetRequiredService<InitializerHostedService>());
            services.AddHostedService<MoveMessagesHostedService>();
            services.AddHostedService<CachedAdminViewModelService>();

            services.AddSingleton(services => new InitializerHostedService(
                services,
                services.GetRequiredService<ITelemetryClient>(),
                services.GetRequiredService<ILogger<InitializerHostedService>>()));

            return services;
        }
    }
}

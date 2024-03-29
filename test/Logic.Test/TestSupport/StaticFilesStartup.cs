// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace NuGet.Insights
{
    public class StaticFilesStartup
    {
        public StaticFilesStartup(IConfiguration configuration)
        {
            Configuration = configuration;
            _paths = new ConcurrentQueue<string>();
        }

        public IConfiguration Configuration { get; }

        private readonly ConcurrentQueue<string> _paths;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(_paths);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseMiddleware<RecordPathMiddleware>(_paths);
            app.UseDeveloperExceptionPage();
            app.UseStaticFiles(new StaticFileOptions
            {
                ServeUnknownFileTypes = true,
            });
        }

        private class RecordPathMiddleware
        {
            private readonly RequestDelegate _next;
            private readonly ConcurrentQueue<string> _paths;

            public RecordPathMiddleware(RequestDelegate next, ConcurrentQueue<string> paths)
            {
                _next = next;
                _paths = paths;
            }

            public async Task InvokeAsync(HttpContext context)
            {
                _paths.Enqueue(context.Request.Path);
                await _next(context);
            }
        }
    }
}

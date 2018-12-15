using System.Net;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Website.Logic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Converters;
using NuGet.Protocol.Core.Types;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace Knapcode.ExplorePackages.Website
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        
        public void ConfigureServices(IServiceCollection services)
        {
            // Completely disable the database.
            EntityContext.Enabled = false;

            // Allow 32 concurrent outgoing connections.
            ServicePointManager.DefaultConnectionLimit = 32;

            // Set the user agent for the HTTP client.
            var userAgentStringBuilder = new UserAgentStringBuilder("Knapcode.ExplorePackages.Website.Bot");
            UserAgent.SetUserAgentString(userAgentStringBuilder);

            // Enable ExplorePackages dependencies.
            var explorePackagesSettings = new ExplorePackagesSettings();
            Configuration.Bind("ExplorePackages", explorePackagesSettings);
            services.AddExplorePackages(explorePackagesSettings);

            // Add stuff specific to the website.
            services.AddLogging();
            services.AddSingleton<IHostedService, SearchSearchUrlCacheRefresher>();
            services.AddMvc();
            services
                .AddSignalR()
                .AddJsonProtocol(o =>
                {
                    o.PayloadSerializerSettings.Converters.Add(new StringEnumConverter());
                });
        }
        
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseSignalR(routes =>
            {
                routes.MapHub<PackageReportHub>(PackageReportHub.Path);
            });

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");

                routes.MapRoute(
                    name: "explore",
                    template: "{controller=Home}/{action=Explore}/{id}/{version}");
            });
        }
    }
}

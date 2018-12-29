using System.Collections.Generic;
using System.Net;
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
        
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            // Allow 32 concurrent outgoing connections.
            ServicePointManager.DefaultConnectionLimit = 32;

            // Set the user agent for the HTTP client.
            var userAgentStringBuilder = new UserAgentStringBuilder("Knapcode.ExplorePackages.Website.Bot");
            UserAgent.SetUserAgentString(userAgentStringBuilder);

            // Add the base configuration.
            var configurationBuilder = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Knapcode.ExplorePackages:DatabaseType", "None" }
                });
            var configuration = configurationBuilder.Build();
            serviceCollection.Configure<ExplorePackagesSettings>(configuration.GetSection("Knapcode.ExplorePackages"));

            // Enable ExplorePackages dependencies.
            serviceCollection.AddExplorePackages();

            // Add stuff specific to the website.
            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<IHostedService, SearchSearchUrlCacheRefresher>();
            serviceCollection.AddMvc();
            serviceCollection
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

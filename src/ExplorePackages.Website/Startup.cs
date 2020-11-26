using System.Collections.Generic;
using System.Text.Json.Serialization;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Website.Logic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            // Add the base configuration.
            var configurationBuilder = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Knapcode.ExplorePackages:DatabaseType", "None" }
                });
            var configuration = configurationBuilder.Build();
            serviceCollection.Configure<ExplorePackagesSettings>(configuration.GetSection(ExplorePackagesSettings.DefaultSectionName));

            // Enable ExplorePackages dependencies.
            serviceCollection.AddExplorePackages("Knapcode.ExplorePackages.Website");
            serviceCollection.AddExplorePackagesEntities();
            serviceCollection.AddScoped<ServiceClientFactory>();

            // Add stuff specific to the website.
            serviceCollection.AddLogging();
            serviceCollection.AddMvc();
            serviceCollection
                .AddSignalR()
                .AddJsonProtocol(o =>
                {
                    o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });
        }
        
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(routes =>
            {
                routes.MapHub<PackageReportHub>(PackageReportHub.Path);

                routes.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");

                routes.MapControllerRoute(
                    name: "explore",
                    pattern: "{controller=Home}/{action=Explore}/{id}/{version}");
            });
        }
    }
}

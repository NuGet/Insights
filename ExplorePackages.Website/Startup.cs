using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Support;
using Knapcode.ExplorePackages.Website.Logic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Converters;

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

            // Enable ExplorePackages dependencies.
            var explorePackagesSettings = new ExplorePackagesSettings();
            Configuration.Bind("ExplorePackages", explorePackagesSettings);
            services.AddExplorePackages(explorePackagesSettings);

            // Add stuff specific to the website.
            services.AddLogging();
            services.AddTransient<NuGet.Common.ILogger, NuGetLogger>();
            services.AddMvc();
            services.AddSignalR(o =>
            {
                o.JsonSerializerSettings.Converters.Add(new StringEnumConverter());
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
            });
        }
    }
}
